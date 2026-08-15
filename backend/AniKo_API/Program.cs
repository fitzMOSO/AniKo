var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.Run();

// Note: the .NET 10 Web SDK already emits a *public* Program class for top-level
// statements, so WebApplicationFactory<Program> resolves without any help here.
// Do not add `public partial class Program;` — the generated class is not declared
// partial, so a second declaration is a duplicate-definition error.
