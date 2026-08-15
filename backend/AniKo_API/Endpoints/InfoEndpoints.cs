namespace AniKo_API.Endpoints;

/// <summary>
/// Service identity. Answers "which build is this, and what is it talking to?"
/// without needing log access.
/// </summary>
public static class InfoEndpoints
{
    /// <summary>Reported by <c>/</c>. Phase C replaces this with "PostgreSQL".</summary>
    public const string DataStore = "None (skeleton)";

    public static IEndpointRouteBuilder MapInfoEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/", () => TypedResults.Ok(new
        {
            name = "AniKo API",
            version = typeof(InfoEndpoints).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            status = "Running",
            dataStore = DataStore,
            timestamp = DateTime.UtcNow,
        }))
        .WithName("Root")
        .WithTags("Info")
        .ExcludeFromDescription();

        return routes;
    }
}
