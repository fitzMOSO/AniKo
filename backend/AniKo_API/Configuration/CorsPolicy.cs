namespace AniKo_API.Configuration;

/// <summary>
/// Builds the browser-facing CORS policy from configuration.
/// <para>
/// This exists because the frontend is deployed on Netlify and the API on Render, which makes
/// every call from the dashboard a cross-origin request. CORS failures are unusually expensive to
/// diagnose: the server returns a perfectly good 200, <c>curl</c> works, the logs look healthy,
/// and only the browser refuses the response — so the symptom appears in the frontend and the
/// cause lives here.
/// </para>
/// </summary>
public static class CorsPolicy
{
    /// <summary>The single named policy. Named rather than default so it must be opted into.</summary>
    public const string PolicyName = "AniKoFrontend";

    /// <summary>Configuration key holding the allowed origin list.</summary>
    public const string OriginsKey = "Cors:AllowedOrigins";

    /// <summary>
    /// Reads and validates the configured origins.
    /// </summary>
    /// <remarks>
    /// An origin is scheme + host + optional port, and nothing else. A value carrying a path
    /// (<c>https://site.netlify.app/overview</c>) or a trailing slash is the mistake worth
    /// guarding, because it is what a person copies out of a browser address bar and because
    /// ASP.NET Core does not complain about it — <c>WithOrigins</c> compares the request's
    /// <c>Origin</c> header for an exact string match, so a path simply never matches and every
    /// request is blocked with no error anywhere on the server. Failing loudly at startup turns a
    /// silent, browser-only outage into a message next to the cause.
    /// </remarks>
    public static string[] ResolveOrigins(IConfiguration configuration)
    {
        var origins = configuration.GetSection(OriginsKey).Get<string[]>() ?? [];

        foreach (var origin in origins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException(
                    $"{OriginsKey} contains '{origin}', which is not an absolute URI. " +
                    "An allowed origin looks like 'https://example.com'.");
            }

            if (origin.EndsWith('/') || uri.AbsolutePath != "/")
            {
                throw new InvalidOperationException(
                    $"{OriginsKey} contains '{origin}', which includes a path or a trailing slash. " +
                    "A CORS origin is scheme + host + optional port only — no path, no trailing " +
                    $"slash. Use '{uri.Scheme}://{uri.Authority}'. This is rejected at startup " +
                    "because ASP.NET Core would accept it and then silently match nothing.");
            }
        }

        return origins;
    }
}
