using Npgsql;

namespace AniKo_API.Configuration;

/// <summary>
/// Bridges the one shape mismatch between Render and Npgsql: Render hands the application a
/// Postgres connection <em>URI</em>, while Npgsql only understands a keyword connection string.
/// Isolated here so the conversion is unit-testable without a deploy, and so the "no database
/// configured" case fails at startup instead of at the first request.
/// </summary>
public static class ConnectionStringResolver
{
    /// <summary>
    /// The platform-supplied URI. Render populates this from a blueprint's
    /// <c>fromDatabase: { property: connectionString }</c> binding, or by hand in the dashboard.
    /// </summary>
    public const string PlatformUriKey = "DATABASE_URL";

    /// <summary>The local-development path: an ordinary keyword string in appsettings.</summary>
    public const string FallbackKey = "ConnectionStrings:DefaultConnection";

    private const int DefaultPostgresPort = 5432;

    /// <summary>Applied when the URI says nothing about SSL. See <see cref="ResolveSslMode"/>.</summary>
    private const SslMode DefaultSslMode = SslMode.Require;

    /// <summary>
    /// Marks a value that is already an Npgsql keyword string. Every usable Npgsql connection
    /// string names a host, so this is the cheapest reliable discriminator against a URI —
    /// and it means a hand-written keyword string in DATABASE_URL is honoured rather than
    /// rejected by the URI parser.
    /// </summary>
    private const string KeywordStringMarker = "Host=";

    /// <summary>
    /// Produces the connection string Npgsql should use, preferring the platform URI so that a
    /// deployed instance can never accidentally fall through to a developer's local database.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when nothing is configured, or when the configured value is neither a keyword
    /// string nor a Postgres URI. Both are deploy-time mistakes: failing loudly at startup is
    /// the point, because the alternative — defaulting to localhost — produces a service that
    /// starts healthy and then fails on every request that touches data.
    /// </exception>
    public static string Resolve(IConfiguration configuration)
    {
        var platformUri = configuration[PlatformUriKey];

        // An environment variable declared without a value arrives as "", not as absent, so a
        // null check alone would treat an empty DATABASE_URL as configured and then fail parsing.
        var configured = string.IsNullOrWhiteSpace(platformUri)
            ? configuration[FallbackKey]
            : platformUri;

        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"No database connection is configured. Set the '{PlatformUriKey}' environment variable " +
                $"(Render supplies this when a Postgres instance is attached to the service), or set " +
                $"'{FallbackKey}' in appsettings for local development.");
        }

        configured = configured.Trim();

        return configured.Contains(KeywordStringMarker, StringComparison.OrdinalIgnoreCase)
            ? configured
            : ConvertUri(configured);
    }

    private static string ConvertUri(string uriValue)
    {
        // Note what is deliberately absent from every throw below: the value itself. It carries
        // the database password, and exception messages end up in Render's log stream.
        if (!Uri.TryCreate(uriValue, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                $"The value of '{PlatformUriKey}' is neither an Npgsql keyword connection string " +
                $"(recognised by '{KeywordStringMarker}') nor an absolute URI.");
        }

        // Render's dashboard and its docs are not consistent about which of the two interchangeable
        // Postgres URI schemes they show, so both are accepted.
        if (!uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The value of '{PlatformUriKey}' uses the '{uri.Scheme}' scheme; " +
                $"expected 'postgresql' or 'postgres'.");
        }

        var database = Decode(uri.AbsolutePath.TrimStart('/'));

        if (string.IsNullOrEmpty(database))
        {
            throw new InvalidOperationException(
                $"The URI in '{PlatformUriKey}' names no database. Expected the form " +
                $"'postgresql://user:password@host:port/database'.");
        }

        var userInfo = uri.UserInfo;

        if (string.IsNullOrEmpty(userInfo))
        {
            throw new InvalidOperationException(
                $"The URI in '{PlatformUriKey}' carries no username. Expected the form " +
                $"'postgresql://user:password@host:port/database'.");
        }

        // Split on the first ':' only — a password may legitimately contain an encoded colon,
        // and splitting on all of them would silently truncate it.
        var separator = userInfo.IndexOf(':');
        var username = separator < 0 ? userInfo : userInfo[..separator];
        var password = separator < 0 ? string.Empty : userInfo[(separator + 1)..];

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,

            // Uri reports -1 rather than 5432 for an unknown scheme with no explicit port, and
            // Render's docs note the port is usually omitted because it is always the default.
            Port = uri.Port == -1 ? DefaultPostgresPort : uri.Port,
            Database = database,
            Username = Decode(username),
            Password = Decode(password),
            SslMode = ResolveSslMode(uri)
        };

        // The builder, rather than string concatenation: a generated password containing ';' or
        // '=' would otherwise truncate or corrupt the keyword string it is embedded in.
        return builder.ConnectionString;
    }

    /// <summary>
    /// Uri.UnescapeDataString, not the form-encoding decoder: the latter maps '+' to a space,
    /// which would silently corrupt any generated password containing a literal '+' into an
    /// authentication failure with no useful diagnostic.
    /// </summary>
    private static string Decode(string value) =>
        value.Length == 0 ? value : Uri.UnescapeDataString(value);

    /// <summary>
    /// Derives SSL from the URI where it says something, and otherwise defaults to Require.
    /// <para>
    /// Known: Render's Postgres docs state that "external connections to your database are
    /// encrypted in transit using Render-managed TLS certificates" — scoped explicitly to
    /// external connections. Assumed: that TLS is also available on the private-network
    /// (internal) hostname a blueprint's <c>connectionString</c> property resolves to; Render's
    /// docs are silent on this, so it is not asserted as fact here.
    /// </para>
    /// <para>
    /// Getting the default wrong in either direction is safe to discover: requiring SSL against a
    /// server that does not offer it fails the connection outright, and omitting it against a
    /// server that demands it fails just as immediately. Both are loud. What is *not* loud is
    /// SslMode=Prefer, which silently downgrades to an unencrypted connection — over the public
    /// internet, for the external hostname — and reports nothing. That asymmetry is why Require
    /// is the default and Prefer is not.
    /// </para>
    /// <para>
    /// Require is deliberately not paired with Trust Server Certificate. Since Npgsql 8.0
    /// ("SSL Mode=Require no longer validates certificates"), Require mandates encryption without
    /// validating the certificate, so the flag is unnecessary — setting it would only suppress
    /// validation that is not happening. Upgrading to VerifyCA/VerifyFull is the real security
    /// win, but it depends on Render's CA chain being present in the container's trust store,
    /// which is unverified; a caller who has confirmed it can request it via '?sslmode=verify-full'.
    /// </para>
    /// <para>
    /// An unrecognised sslmode throws rather than falling back to the default. Someone who writes
    /// 'verify-fulll' is asking for certificate verification; silently giving them Require — which
    /// does not validate certificates at all — turns a typo into a security downgrade nobody sees.
    /// A failed deploy is loud and fixable in one commit; a connection that is encrypted but
    /// unauthenticated looks identical to a correct one from the outside.
    /// </para>
    /// </summary>
    private static SslMode ResolveSslMode(Uri uri)
    {
        var sslmode = System.Web.HttpUtility.ParseQueryString(uri.Query)["sslmode"];

        return sslmode?.Trim().ToLowerInvariant() switch
        {
            "disable" => SslMode.Disable,
            "allow" => SslMode.Allow,
            "prefer" => SslMode.Prefer,
            "require" => SslMode.Require,
            "verify-ca" => SslMode.VerifyCA,
            "verify-full" => SslMode.VerifyFull,

            // Naming the value is safe here, unlike elsewhere in this class: an sslmode is not a
            // secret, and an error that withholds the typo it is complaining about is unusable.
            { } unrecognised => throw new InvalidOperationException(
                $"The URI in '{PlatformUriKey}' specifies sslmode='{unrecognised}', which is not a " +
                $"recognised value. Use one of: disable, allow, prefer, require, verify-ca, verify-full. " +
                $"Omitting sslmode entirely selects '{DefaultSslMode}'."),

            null => DefaultSslMode
        };
    }
}
