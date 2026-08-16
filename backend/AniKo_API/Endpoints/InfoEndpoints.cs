using AniKo_API.Configuration;

namespace AniKo_API.Endpoints;

/// <summary>
/// Service identity. Answers "which build is this, and what is it talking to?"
/// without needing log access.
/// </summary>
public static class InfoEndpoints
{
    /// <summary>
    /// Reported by <c>/</c>.
    /// </summary>
    /// <remarks>
    /// This said <c>"None (skeleton)"</c> with a note that Phase C would replace it — and Phase C
    /// came and went with the constant untouched, so for three phases the one endpoint whose job
    /// is to answer "what is this talking to?" without log access confidently answered "nothing"
    /// while serving data out of Postgres. Nothing failed, which is why it survived: a stale
    /// constant is indistinguishable from a correct one until someone acts on it.
    /// </remarks>
    public const string DataStore = "PostgreSQL";

    public static IEndpointRouteBuilder MapInfoEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/", (IConfiguration configuration) => TypedResults.Ok(new
        {
            name = "AniKo API",
            version = typeof(InfoEndpoints).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            status = "Running",
            dataStore = DataStore,

            // The assembly version above is static across every build, so it cannot
            // answer "which code is this?". The commit can, and that turned out to be
            // the question that mattered: a deploy of the right commit once served the
            // previous one's binary while reporting itself live and healthy.
            commit = PlatformEnvironment.GetBuildCommit(configuration),
            timestamp = DateTime.UtcNow,
        }))
        .WithName("Root")
        .WithTags("Info")
        .ExcludeFromDescription();

        return routes;
    }
}
