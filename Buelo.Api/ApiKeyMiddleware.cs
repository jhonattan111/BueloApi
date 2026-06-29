namespace Buelo.Api;

/// <summary>
/// Optional API-key gate (handoff §12, self-hosted security model). When <c>Buelo:ApiKey</c> is
/// configured, every request must carry <c>Authorization: Bearer &lt;key&gt;</c>; otherwise auth is
/// off (opt-in). <c>/ping</c> stays public, and OpenAPI is allowed in Development.
/// </summary>
public sealed class ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, IHostEnvironment environment)
{
    public async Task Invoke(HttpContext context)
    {
        var apiKey = configuration["Buelo:ApiKey"];
        var path = context.Request.Path.Value ?? string.Empty;

        var isPublic = string.IsNullOrWhiteSpace(apiKey)
            || path.StartsWith("/ping", StringComparison.OrdinalIgnoreCase)
            || (environment.IsDevelopment() && (path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase)
                                                || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)));

        if (isPublic || IsAuthorized(context, apiKey!))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid API key." });
    }

    private static bool IsAuthorized(HttpContext context, string apiKey)
    {
        var header = context.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            && string.Equals(header["Bearer ".Length..].Trim(), apiKey, StringComparison.Ordinal);
    }
}
