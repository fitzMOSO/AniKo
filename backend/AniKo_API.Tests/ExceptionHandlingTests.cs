using System.Text.Json;
using AniKo_API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace AniKo_API.Tests;

public class ExceptionHandlingTests
{
    private static async Task<(int Status, string Body)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/boom";
        context.Response.Body = new MemoryStream();

        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled, "The handler must report that it handled the exception.");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        return (context.Response.StatusCode, body);
    }

    [Fact]
    public async Task UnhandledException_Returns500()
    {
        var (status, _) = await HandleAsync(new InvalidOperationException("boom"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
    }

    [Fact]
    public async Task UnhandledException_DoesNotLeakTheExceptionMessage()
    {
        var (_, body) = await HandleAsync(new InvalidOperationException("SECRET-INTERNAL-DETAIL"));

        Assert.DoesNotContain("SECRET-INTERNAL-DETAIL", body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }

    [Fact]
    public async Task UnhandledException_ReturnsProblemDetailsShape()
    {
        var (_, body) = await HandleAsync(new InvalidOperationException("boom"));

        var problem = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.Equal(
            "An error occurred while processing your request",
            problem.GetProperty("title").GetString());
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.Equal("/boom", problem.GetProperty("instance").GetString());
    }
}
