namespace AniKo_API.Configuration;

/// <summary>
/// Isolates the facts about running on Render: the port is assigned by the platform,
/// and TLS terminates at the edge rather than in this process.
/// Kept in one place so both are unit-testable without a deploy.
/// </summary>
public static class PlatformEnvironment
{
    /// <summary>Render sets RENDER=true on every service it runs.</summary>
    public static bool IsHosted(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["RENDER"]);

    /// <summary>
    /// The URL Kestrel should bind, or null to keep the default.
    /// Returns null rather than throwing on a malformed PORT: an unusable value
    /// should degrade to the default, not crash with a Kestrel-shaped error.
    /// </summary>
    public static string? GetListenUrl(IConfiguration configuration)
    {
        var raw = configuration["PORT"];

        if (!int.TryParse(raw, out var port) || port is < 1 or > 65535)
        {
            return null;
        }

        return $"http://0.0.0.0:{port}";
    }
}
