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
}
