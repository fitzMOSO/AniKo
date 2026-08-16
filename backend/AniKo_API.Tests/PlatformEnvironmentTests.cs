using AniKo_API.Configuration;
using Microsoft.Extensions.Configuration;

namespace AniKo_API.Tests;

public class PlatformEnvironmentTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v =>
                new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void IsHosted_IsFalse_WhenRenderVariableAbsent()
    {
        Assert.False(PlatformEnvironment.IsHosted(Config()));
    }

    [Fact]
    public void IsHosted_IsTrue_WhenRenderVariablePresent()
    {
        Assert.True(PlatformEnvironment.IsHosted(Config(("RENDER", "true"))));
    }

    [Fact]
    public void GetListenUrl_IsNull_WhenNoPortAssigned()
    {
        Assert.Null(PlatformEnvironment.GetListenUrl(Config()));
    }

    [Fact]
    public void GetListenUrl_BindsAllInterfaces_OnAssignedPort()
    {
        Assert.Equal("http://0.0.0.0:10000", PlatformEnvironment.GetListenUrl(Config(("PORT", "10000"))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-port")]
    [InlineData("0")]
    [InlineData("70000")]
    public void GetListenUrl_IsNull_WhenPortIsNotUsable(string port)
    {
        Assert.Null(PlatformEnvironment.GetListenUrl(Config(("PORT", port))));
    }

    [Fact]
    public void GetBuildCommit_ReportsLocal_WhenRenderCommitAbsent()
    {
        // Deliberately not an empty string or a fake sha: this value is read when
        // someone already suspects the wrong code is deployed, so "I do not know"
        // has to be distinguishable from an answer.
        Assert.Equal(PlatformEnvironment.UnknownBuild, PlatformEnvironment.GetBuildCommit(Config()));
    }

    [Fact]
    public void GetBuildCommit_ReportsLocal_WhenRenderCommitIsBlank()
    {
        Assert.Equal(
            PlatformEnvironment.UnknownBuild,
            PlatformEnvironment.GetBuildCommit(Config(("RENDER_GIT_COMMIT", "   "))));
    }

    [Fact]
    public void GetBuildCommit_ShortensToSevenCharacters()
    {
        Assert.Equal(
            "cbc2b73",
            PlatformEnvironment.GetBuildCommit(
                Config(("RENDER_GIT_COMMIT", "cbc2b733a64b5abfc4aeb9ac554742a43d486528"))));
    }

    [Fact]
    public void GetBuildCommit_DoesNotThrow_OnAValueShorterThanSevenCharacters()
    {
        // The value comes from the environment, so it can be anything. This endpoint
        // must answer when things are wrong, which is exactly when a hand-set or
        // truncated variable is most likely.
        Assert.Equal("abc", PlatformEnvironment.GetBuildCommit(Config(("RENDER_GIT_COMMIT", "abc"))));
    }
}
