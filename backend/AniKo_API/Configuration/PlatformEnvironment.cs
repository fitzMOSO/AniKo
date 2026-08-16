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

    /// <summary>Reported by <c>/</c> so a deployed build can be identified without log access.</summary>
    public const string UnknownBuild = "local";

    /// <summary>
    /// The commit this instance was built from, shortened, or <see cref="UnknownBuild"/>
    /// when nothing supplied one.
    /// </summary>
    /// <remarks>
    /// This exists because of a specific failure: Render reported a deploy of the right
    /// commit as "live", the build log showed the publish step genuinely running, the
    /// service was up and healthy — and it served the previous commit's binary. Redeploying
    /// the same commit with the build cache cleared fixed it, so a stale layer had been
    /// reused. Nothing about that is visible from outside: a stale deploy and a good one
    /// answer every health check identically, and the only reason it was caught at all is
    /// that a route added in that commit 404'd.
    ///
    /// Reporting the commit turns "is the deploy real?" into one request. Render sets
    /// RENDER_GIT_COMMIT on every service it runs; absent it, this is a local run and says
    /// so rather than inventing a value, because a wrong commit here would be worse than
    /// none — it is the thing you check when you already distrust what is deployed.
    /// </remarks>
    public static string GetBuildCommit(IConfiguration configuration)
    {
        var sha = configuration["RENDER_GIT_COMMIT"];

        if (string.IsNullOrWhiteSpace(sha))
        {
            return UnknownBuild;
        }

        // Trimmed to the conventional short form. Substring is guarded because the value
        // arrives from the environment: a truncated or hand-set variable must not throw
        // here, since this endpoint's whole purpose is answering when things are wrong.
        var trimmed = sha.Trim();
        return trimmed.Length <= 7 ? trimmed : trimmed[..7];
    }

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
