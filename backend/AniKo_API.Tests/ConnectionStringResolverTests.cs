using AniKo_API.Configuration;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AniKo_API.Tests;

/// <summary>
/// The resolver is the only thing standing between a Render-supplied URI and Npgsql,
/// and every failure it prevents only shows up at deploy time. These tests are the
/// deploy rehearsal: each one encodes a URI shape Render has actually been observed to emit.
/// </summary>
public class ConnectionStringResolverTests
{
    private const string RenderStyleUri =
        "postgresql://aniko_user:s3cret@dpg-abc123-a.oregon-postgres.render.com:5432/aniko_db";

    private static IConfiguration Configure(params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    /// <summary>
    /// Asserting on the raw string would couple the tests to Npgsql's keyword ordering and
    /// quoting rules; parsing it back asserts the only thing that matters — what Npgsql reads.
    /// </summary>
    private static NpgsqlConnectionStringBuilder Parse(string connectionString) => new(connectionString);

    [Fact]
    public void MapsEveryComponentOfAPostgresqlUri()
    {
        var result = Parse(ConnectionStringResolver.Resolve(
            Configure((ConnectionStringResolver.PlatformUriKey, RenderStyleUri))));

        Assert.Equal("dpg-abc123-a.oregon-postgres.render.com", result.Host);
        Assert.Equal(5432, result.Port);
        Assert.Equal("aniko_db", result.Database);
        Assert.Equal("aniko_user", result.Username);
        Assert.Equal("s3cret", result.Password);
    }

    [Fact]
    public void AcceptsThePostgresSchemeAsWellAsPostgresql()
    {
        var result = Parse(ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.PlatformUriKey, "postgres://u:p@db.example.com:5433/appdb"))));

        Assert.Equal("db.example.com", result.Host);
        Assert.Equal(5433, result.Port);
        Assert.Equal("appdb", result.Database);
    }

    [Fact]
    public void DecodesAPercentEncodedPassword()
    {
        // Render-generated passwords routinely contain characters that are illegal raw in a URI
        // userinfo segment; handing the still-encoded form to Npgsql yields an auth failure.
        var result = Parse(ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.PlatformUriKey,
             "postgresql://u:p%40ss%3Aw0rd%2F%23@host.example.com:5432/db"))));

        Assert.Equal("p@ss:w0rd/#", result.Password);
    }

    [Fact]
    public void DecodesAPercentEncodedUsername()
    {
        var result = Parse(ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.PlatformUriKey,
             "postgresql://user%40tenant:pw@host.example.com:5432/db"))));

        Assert.Equal("user@tenant", result.Username);
    }

    [Fact]
    public void TreatsPlusInAPasswordAsALiteralPlus()
    {
        // Guards the form-encoding decoder (which maps '+' to a space) from being used here:
        // a password of "a+b" silently becoming "a b" is an auth failure with no useful diagnostic.
        var result = Parse(ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.PlatformUriKey, "postgresql://u:a+b@host.example.com:5432/db"))));

        Assert.Equal("a+b", result.Password);
    }

    [Fact]
    public void SurvivesAPasswordContainingAConnectionStringDelimiter()
    {
        // A ';' or '=' in the password would truncate or corrupt a hand-concatenated keyword string.
        var result = Parse(ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.PlatformUriKey,
             "postgresql://u:pa%3Bss%3Dword@host.example.com:5432/db"))));

        Assert.Equal("pa;ss=word", result.Password);
        Assert.Equal("db", result.Database);
    }

    [Fact]
    public void DefaultsToPort5432WhenTheUriOmitsIt()
    {
        var result = Parse(ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.PlatformUriKey, "postgresql://u:p@host.example.com/db"))));

        Assert.Equal(5432, result.Port);
        Assert.Equal("host.example.com", result.Host);
    }

    [Fact]
    public void KeepsANonDefaultPort()
    {
        var result = Parse(ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.PlatformUriKey, "postgresql://u:p@host.example.com:6543/db"))));

        Assert.Equal(6543, result.Port);
    }

    [Fact]
    public void RequiresSslForUriSuppliedConnections()
    {
        var result = Parse(ConnectionStringResolver.Resolve(
            Configure((ConnectionStringResolver.PlatformUriKey, RenderStyleUri))));

        Assert.Equal(SslMode.Require, result.SslMode);
    }

    [Fact]
    public void RequiresSslForAnInternalRenderHostnameToo()
    {
        // Pins the decision that the SSL default does not vary with the shape of the hostname:
        // the resolver has no verified way to tell an internal endpoint from an external one,
        // so guessing from the hostname would be a silent downgrade dressed up as a heuristic.
        var result = Parse(ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.PlatformUriKey, "postgresql://u:p@dpg-abc123-a/aniko_db"))));

        Assert.Equal(SslMode.Require, result.SslMode);
        Assert.Equal("dpg-abc123-a", result.Host);
    }

    [Theory]
    [InlineData("disable", SslMode.Disable)]
    [InlineData("prefer", SslMode.Prefer)]
    [InlineData("require", SslMode.Require)]
    [InlineData("verify-ca", SslMode.VerifyCA)]
    [InlineData("verify-full", SslMode.VerifyFull)]
    public void HonoursAnExplicitSslModeInTheUriQuery(string libpqValue, SslMode expected)
    {
        var result = Parse(ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.PlatformUriKey,
             $"postgresql://u:p@host.example.com:5432/db?sslmode={libpqValue}"))));

        Assert.Equal(expected, result.SslMode);
    }

    [Theory]
    [InlineData("verify-fulll")]
    [InlineData("verifyfull")]
    [InlineData("true")]
    [InlineData("")]
    public void ThrowsOnAnUnrecognisedSslMode(string libpqValue)
    {
        // The failure mode this prevents is specific: a typo'd 'verify-full' quietly resolving to
        // Require, which on Npgsql 8+ encrypts without validating the certificate at all. The
        // caller asked for authentication and would have silently received none.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ConnectionStringResolver.Resolve(Configure(
                (ConnectionStringResolver.PlatformUriKey,
                 $"postgresql://u:p@host.example.com:5432/db?sslmode={libpqValue}"))));

        // Asserting on a mode name that is not a substring of any input above, so the test cannot
        // pass just because the rejected value happens to echo back into the message.
        Assert.Contains("verify-ca", exception.Message);
    }

    [Fact]
    public void AcceptsAnSslModeRegardlessOfCasingOrSurroundingWhitespace()
    {
        var result = Parse(ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.PlatformUriKey,
             "postgresql://u:p@host.example.com:5432/db?sslmode=Verify-Full"))));

        Assert.Equal(SslMode.VerifyFull, result.SslMode);
    }

    [Fact]
    public void NamesTheOffendingSslModeInTheError()
    {
        // Unlike the URI itself, an sslmode is not a secret — and an error that withholds the
        // typo it is complaining about sends the reader back to the dashboard to guess.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ConnectionStringResolver.Resolve(Configure(
                (ConnectionStringResolver.PlatformUriKey,
                 "postgresql://u:p@host.example.com:5432/db?sslmode=verify-fulll"))));

        Assert.Contains("verify-fulll", exception.Message);
    }

    [Fact]
    public void IgnoresQueryParametersThatAreNotSslMode()
    {
        var result = Parse(ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.PlatformUriKey,
             "postgresql://u:p@host.example.com:5432/db?connect_timeout=10"))));

        Assert.Equal("db", result.Database);
        Assert.Equal(SslMode.Require, result.SslMode);
    }

    [Fact]
    public void FallsBackToDefaultConnectionWhenThePlatformUriIsAbsent()
    {
        const string local = "Host=localhost;Port=5432;Database=aniko;Username=postgres;Password=postgres";

        var result = ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.FallbackKey, local)));

        Assert.Equal(local, result);
    }

    [Fact]
    public void FallsBackWhenThePlatformUriIsPresentButBlank()
    {
        // An env var declared with no value arrives as "" rather than absent; treating that as
        // configured would throw a parse error instead of using the local connection string.
        const string local = "Host=localhost;Database=aniko;Username=postgres;Password=postgres";

        var result = ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.PlatformUriKey, "   "),
            (ConnectionStringResolver.FallbackKey, local)));

        Assert.Equal(local, result);
    }

    [Fact]
    public void PrefersThePlatformUriOverTheLocalFallback()
    {
        var result = Parse(ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.PlatformUriKey, RenderStyleUri),
            (ConnectionStringResolver.FallbackKey, "Host=localhost;Database=aniko"))));

        Assert.Equal("dpg-abc123-a.oregon-postgres.render.com", result.Host);
    }

    [Fact]
    public void PassesAKeywordConnectionStringThroughUnchanged()
    {
        const string keyword = "Host=dpg-abc123-a;Port=5432;Database=aniko;Username=u;Password=p;SSL Mode=Require";

        var result = ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.PlatformUriKey, keyword)));

        Assert.Equal(keyword, result);
    }

    [Fact]
    public void RecognisesAKeywordConnectionStringRegardlessOfCasing()
    {
        const string keyword = "host=localhost;database=aniko;username=postgres;password=postgres";

        var result = ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.FallbackKey, keyword)));

        Assert.Equal(keyword, result);
    }

    [Fact]
    public void ThrowsNamingBothKeysWhenNothingIsConfigured()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ConnectionStringResolver.Resolve(Configure()));

        Assert.Contains(ConnectionStringResolver.PlatformUriKey, exception.Message);
        Assert.Contains(ConnectionStringResolver.FallbackKey, exception.Message);
    }

    [Fact]
    public void ThrowsWhenBothValuesAreBlank()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ConnectionStringResolver.Resolve(Configure(
                (ConnectionStringResolver.PlatformUriKey, ""),
                (ConnectionStringResolver.FallbackKey, "   "))));

        Assert.Contains(ConnectionStringResolver.PlatformUriKey, exception.Message);
    }

    [Fact]
    public void ThrowsOnAUriWithAnUnsupportedScheme()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ConnectionStringResolver.Resolve(Configure(
                (ConnectionStringResolver.PlatformUriKey, "mysql://u:p@host.example.com:3306/db"))));

        Assert.Contains("mysql", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThrowsOnAUriWithNoDatabaseSegment()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ConnectionStringResolver.Resolve(Configure(
                (ConnectionStringResolver.PlatformUriKey, "postgresql://u:p@host.example.com:5432"))));
    }

    [Fact]
    public void ThrowsOnAUriWithNoCredentials()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ConnectionStringResolver.Resolve(Configure(
                (ConnectionStringResolver.PlatformUriKey, "postgresql://host.example.com:5432/db"))));
    }

    [Fact]
    public void NeverEchoesTheSecretBearingValueInAnErrorMessage()
    {
        // Exception messages land in Render's log stream, which is not a secret store.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ConnectionStringResolver.Resolve(Configure(
                (ConnectionStringResolver.PlatformUriKey, "mysql://u:sup3rs3cret@host.example.com:3306/db"))));

        Assert.DoesNotContain("sup3rs3cret", exception.Message);
    }

    [Fact]
    public void AcceptsAUriWithNoPasswordAtAll()
    {
        var result = Parse(ConnectionStringResolver.Resolve(Configure(
            (ConnectionStringResolver.PlatformUriKey, "postgresql://trusteduser@host.example.com:5432/db"))));

        Assert.Equal("trusteduser", result.Username);
        Assert.True(string.IsNullOrEmpty(result.Password));
    }
}
