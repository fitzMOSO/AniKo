using AniKo_API.Endpoints;
using AniKo_API.Middleware;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

// Mounted in every environment on purpose: a deployed API that cannot be
// browsed is half a deliverable. Revisit if this ever serves private data.
app.MapOpenApi();
app.MapScalarApiReference();

app.UseHttpsRedirection();

app.MapHealthChecks("/health");
app.MapInfoEndpoints();

app.Run();

// Note: the .NET 10 Web SDK already emits a *public* Program class for top-level
// statements, so WebApplicationFactory<Program> resolves without any help here.
// Do not add `public partial class Program;` — the generated class is not declared
// partial, so a second declaration is a duplicate-definition error.
