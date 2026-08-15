# AniKo Backend Phases A & B Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the stock WeatherForecast template in `backend/AniKo_API` into a deployable, tested ASP.NET Core Minimal API skeleton, and get it running on Render before any domain code exists.

**Architecture:** Follows `Minimal_API_Project_Sample` — Minimal API on .NET 10, layered `Data / Models / DTOs / Mappers / Validators / Repositories / Services / Endpoints / Middleware`, endpoints as static extension classes returning `TypedResults`. Phase B deploys the skeleton with **no database at all**, so the only thing a failed deploy can mean is a container, port, or health-check problem. Postgres arrives in Phase C, seeding in Phase D.

**Tech Stack:** .NET 10 (SDK 10.0.203), ASP.NET Core Minimal API, xUnit 2.9.3, `WebApplicationFactory`, Scalar for OpenAPI, Docker, Render.

**Spec:** `plan/backend plan/backend.md` (and its flat mirror `plan/backend plan/CHECKLIST.md`)

> Note: `plan/` is gitignored, so the spec is not tracked in this repo. It lives on disk only. This is a known, unresolved issue flagged to the user.

## Global Constraints

- Target framework is `net10.0`. Do not retarget.
- All packages pinned to these exact versions: `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, `Microsoft.EntityFrameworkCore.Design` 10.0.11, `FluentValidation.DependencyInjectionExtensions` 12.1.1, `Scalar.AspNetCore` 2.16.20, `Microsoft.AspNetCore.OpenApi` 10.0.7, `Microsoft.AspNetCore.Mvc.Testing` 10.0.11.
- Tests use **xUnit 2.9.3**, whatever the `dotnet new xunit` template installs. There is no `xunit3` template in this SDK, and xUnit v3 buys nothing here — `IClassFixture`, `[Fact]`, `[Theory]` and `[InlineData]` are identical across both. Do not hand-migrate to v3.
- `Nullable` and `ImplicitUsings` stay `enable`.
- **No `try/catch` that swallows a startup failure.** Log and rethrow. This is the single most important rule in the plan; a healthy app on a broken database is worse than a failed deploy.
- No phase is complete until it is running on Render. Building and testing locally means staged, not done.
- Every endpoint returns `TypedResults`, never `Results`.
- Commit after every task. Never commit with a failing build or failing tests.
- Endpoint paths must match `plan/dashboard plan/CHECKLIST.md` Phase I character for character. Phases A and B add no `/api` routes, so this constraint only bites from Phase F.
- `Scalar.AspNetCore` is version 2.x here, whereas the sample uses 1.2.42. The v1 fluent options API (`.WithTitle(...).WithTheme(...)`) may not exist. Task 6 uses the no-argument overload deliberately; do not port the sample's options block without checking it compiles.

---

## File Structure

| File | Responsibility |
|---|---|
| `.gitignore` | Repo-wide ignores. Currently contains only `plan`; gains standard .NET rules. |
| `backend/AniKo.slnx` | Solution referencing the API and its test project. Replaces `backend/AniKo_API/AniKo_API.slnx`. |
| `backend/AniKo_API/AniKo_API.csproj` | API project. Gains package references and the layer folders. |
| `backend/AniKo_API/Program.cs` | Composition root only — DI registration, pipeline, endpoint mapping. No handler bodies. |
| `backend/AniKo_API/Configuration/PlatformEnvironment.cs` | Answers "are we hosted on Render?" and "what port were we assigned?" in one testable place. |
| `backend/AniKo_API/Endpoints/InfoEndpoints.cs` | The `/` info endpoint. |
| `backend/AniKo_API/Middleware/GlobalExceptionHandler.cs` | `IExceptionHandler` producing a ProblemDetails body. |
| `backend/AniKo_API.Tests/AniKo_API.Tests.csproj` | Test project. |
| `backend/AniKo_API.Tests/ApiFactory.cs` | Shared `WebApplicationFactory<Program>` for integration tests. |
| `backend/AniKo_API.Tests/HealthEndpointTests.cs` | `/health` and `/` behaviour. |
| `backend/AniKo_API.Tests/PlatformEnvironmentTests.cs` | Unit tests for port and hosted-ness detection. |
| `backend/AniKo_API.Tests/ExceptionHandlingTests.cs` | Unhandled exception produces ProblemDetails, not a stack trace. |
| `Dockerfile` | Repo-root, multi-stage, solution-aware, non-root, one `ENTRYPOINT`. |
| `.dockerignore` | Keeps `bin obj .vs node_modules plan .git frontend` out of the build context. |
| `render.yaml` | Blueprint. Phase B declares the web service only — no database yet. |

---

# Phase A — Project shape & test harness

---

### Task 1: Repo hygiene — stop tracking build artifacts

The repo currently tracks `bin/`, `obj/`, and `.vs/` (including `.suo`, a binary Visual Studio user-state file). `.gitignore` contains a single line: `plan`. Every later task's diff will be unreadable until this is fixed, so it goes first.

**Files:**
- Modify: `.gitignore`

**Interfaces:**
- Consumes: nothing
- Produces: a clean `git status`, so later tasks can use `git add -A` safely

- [x] **Step 1: Confirm the problem**

```bash
git ls-files | grep -E "(/obj/|/bin/|\.vs/)" | head -20
```

Expected: a list of tracked `obj/` and `.vs/` files, including `.suo`.

- [x] **Step 2: Replace `.gitignore`**

Write `.gitignore` at the repo root:

```gitignore
# Planning docs (local only)
plan

# .NET build output
bin/
obj/
[Dd]ebug/
[Rr]elease/
*.user
*.suo
*.userprefs

# Visual Studio / Rider / VS Code
.vs/
.idea/
*.swp

# Test output
TestResults/
*.coverage
coverage/

# Environment
.env
.env.local
*.pfx
```

Note `frontend/` has its own `.gitignore` covering `node_modules` and `dist`; do not duplicate those here.

- [x] **Step 3: Untrack the artifacts without deleting them**

```bash
git rm -r --cached backend/AniKo_API/obj backend/AniKo_API/bin backend/AniKo_API/.vs backend/AniKo_API/AniKo_API.csproj.user
```

Expected: files listed as removed from the index. They remain on disk.

- [x] **Step 4: Verify**

```bash
git status --short
git ls-files | grep -E "(/obj/|/bin/|\.vs/|\.user$)" | wc -l
```

Expected: the second command prints `0`.

- [x] **Step 5: Commit**

```bash
git add -A
git commit -m "chore: add .NET gitignore and untrack build artifacts"
```

---

### Task 2: Strip the template and stand up the solution + test project

The API must build and a test project must run before anything else is added. This task deletes the WeatherForecast sample, creates the test project, and proves the harness works with one trivial test.

`WebApplicationFactory<Program>` needs `Program` to be a reachable type. With top-level statements it is `internal` by default, so a `public partial class Program;` declaration is added at the bottom of `Program.cs`. Without it, Task 3 fails with a confusing generic-constraint error rather than an obvious one.

**Files:**
- Delete: `backend/AniKo_API/Controllers/WeatherForecastController.cs`, `backend/AniKo_API/WeatherForecast.cs`, `backend/AniKo_API/AniKo_API.slnx`
- Modify: `backend/AniKo_API/Program.cs`, `backend/AniKo_API/AniKo_API.http`
- Create: `backend/AniKo.slnx`, `backend/AniKo_API.Tests/AniKo_API.Tests.csproj`, `backend/AniKo_API.Tests/SolutionSmokeTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `public partial class Program` — the entry-point type every integration test resolves through

- [x] **Step 1: Create the test project and solution**

```bash
cd backend
dotnet new xunit -n AniKo_API.Tests -f net10.0 -o AniKo_API.Tests
dotnet new sln -n AniKo --format slnx
dotnet sln AniKo.slnx add AniKo_API/AniKo_API.csproj AniKo_API.Tests/AniKo_API.Tests.csproj
dotnet add AniKo_API.Tests/AniKo_API.Tests.csproj reference AniKo_API/AniKo_API.csproj
rm AniKo_API/AniKo_API.slnx
```

Both commands are verified against SDK 10.0.203: `--format slnx` is supported, and the `xunit` template installs xUnit 2.9.3 with `Microsoft.NET.Test.Sdk` 17.14.1. Accept those versions as-is.

- [x] **Step 2: Write the failing test**

Create `backend/AniKo_API.Tests/SolutionSmokeTests.cs`:

```csharp
namespace AniKo_API.Tests;

public class SolutionSmokeTests
{
    [Fact]
    public void ApiAssemblyIsReferencedAndEntryPointIsPublic()
    {
        var programType = typeof(Program);

        Assert.Equal("Program", programType.Name);
        Assert.True(programType.IsPublic, "Program must be public so WebApplicationFactory can resolve it.");
    }
}
```

- [x] **Step 3: Run it to verify it fails**

```bash
cd backend && dotnet test AniKo.slnx
```

Expected: FAIL — `Program` is inaccessible, reported as a compile error `CS0122` or `CS0246`.

- [x] **Step 4: Delete the template and make `Program` public**

```bash
rm backend/AniKo_API/Controllers/WeatherForecastController.cs backend/AniKo_API/WeatherForecast.cs
rmdir backend/AniKo_API/Controllers
```

Replace `backend/AniKo_API/Program.cs` entirely:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.Run();

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can boot the app in integration tests.
/// Top-level statements generate an internal Program class; this makes it public.
/// </summary>
public partial class Program;
```

Note `AddControllers()`, `UseAuthorization()` and `MapControllers()` are gone. There are no controllers and nothing to authorise; leaving them in implies a design that does not exist.

- [x] **Step 5: Replace the stale `.http` file**

Replace `backend/AniKo_API/AniKo_API.http`:

```http
@AniKo_API_HostAddress = http://localhost:5089

### Health check
GET {{AniKo_API_HostAddress}}/health
Accept: text/plain

###
```

- [x] **Step 6: Run tests to verify they pass**

```bash
cd backend && dotnet test AniKo.slnx
```

Expected: PASS, 1 test.

- [x] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(backend): strip WeatherForecast template, add solution and test project"
```

---

### Task 3: `/health` and `/` info endpoint, tested through the real pipeline

`/health` is what Render polls to decide whether a deploy succeeded, so it is tested through `WebApplicationFactory` — an in-process HTTP request through the real middleware pipeline — rather than by calling a method.

The info endpoint reports the data store as a string. In Phase B that is `"None (skeleton)"`; Phase C changes it to `"PostgreSQL"`. It exists so that opening the deployed root URL immediately answers "which build is this and what is it talking to?"

**Files:**
- Create: `backend/AniKo_API/Endpoints/InfoEndpoints.cs`, `backend/AniKo_API.Tests/ApiFactory.cs`, `backend/AniKo_API.Tests/HealthEndpointTests.cs`
- Modify: `backend/AniKo_API/Program.cs`, `backend/AniKo_API.Tests/AniKo_API.Tests.csproj`

**Interfaces:**
- Consumes: `public partial class Program` from Task 2
- Produces: `InfoEndpoints.MapInfoEndpoints(this IEndpointRouteBuilder)` returning `IEndpointRouteBuilder`; `ApiFactory : WebApplicationFactory<Program>`

- [x] **Step 1: Add the test package**

```bash
cd backend && dotnet add AniKo_API.Tests package Microsoft.AspNetCore.Mvc.Testing --version 10.0.11
```

- [x] **Step 2: Write the shared factory**

Create `backend/AniKo_API.Tests/ApiFactory.cs`:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;

namespace AniKo_API.Tests;

/// <summary>
/// Boots the API in-process for integration tests. Uses the Development environment
/// so behaviour matches a developer's local run rather than the hosted configuration.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }
}
```

- [x] **Step 3: Write the failing tests**

Create `backend/AniKo_API.Tests/HealthEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AniKo_API.Tests;

public class HealthEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HealthEndpointTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Root_ReturnsServiceIdentity()
    {
        var client = _factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/");

        Assert.Equal("AniKo API", payload.GetProperty("name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("version").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("dataStore").GetString()));
    }
}
```

- [x] **Step 4: Run to verify failure**

```bash
cd backend && dotnet test AniKo.slnx
```

Expected: `Health_ReturnsOk` passes (health was mapped in Task 2), `Root_ReturnsServiceIdentity` FAILS with 404.

Both are run together deliberately — a test that passes before you write the code is a test that is not testing your code, and knowing which of the two already passes is information.

- [x] **Step 5: Write the info endpoint**

Create `backend/AniKo_API/Endpoints/InfoEndpoints.cs`:

```csharp
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
```

- [x] **Step 6: Map it in `Program.cs`**

In `backend/AniKo_API/Program.cs`, add the using at the top:

```csharp
using AniKo_API.Endpoints;
```

and replace the line `app.MapHealthChecks("/health");` with:

```csharp
app.MapHealthChecks("/health");
app.MapInfoEndpoints();
```

- [x] **Step 7: Run tests to verify they pass**

```bash
cd backend && dotnet test AniKo.slnx
```

Expected: PASS, 3 tests.

- [x] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(backend): add info endpoint and integration test harness"
```

---

### Task 4: Global exception handling with ProblemDetails

An unhandled exception must not leak a stack trace to a public URL. This is the sample's `GlobalExceptionHandler`, carried over.

**Files:**
- Create: `backend/AniKo_API/Middleware/GlobalExceptionHandler.cs`, `backend/AniKo_API.Tests/ExceptionHandlingTests.cs`
- Modify: `backend/AniKo_API/Program.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `AniKo_API.Middleware.GlobalExceptionHandler : IExceptionHandler`

- [x] **Step 1: Write the failing test**

Create `backend/AniKo_API.Tests/ExceptionHandlingTests.cs`:

```csharp
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
```

This tests the handler directly against a `DefaultHttpContext` rather than through `WebApplicationFactory`. Going through the factory would mean replacing the app's whole middleware pipeline to inject a throwing endpoint, which is fragile and tests ASP.NET's wiring more than it tests this handler. The registration itself is covered by the existing integration tests continuing to pass.

The two `DoesNotContain` assertions are the point. Asserting only on the 500 would pass even if the entire exception were serialised into the response body.

- [x] **Step 2: Run to verify failure**

```bash
cd backend && dotnet test AniKo.slnx --filter ExceptionHandlingTests
```

Expected: FAIL to compile — `GlobalExceptionHandler` does not exist.

- [x] **Step 3: Write the handler**

Create `backend/AniKo_API/Middleware/GlobalExceptionHandler.cs`:

```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AniKo_API.Middleware;

/// <summary>
/// Converts any unhandled exception into an RFC 7807 ProblemDetails response.
/// The exception detail goes to the log, never to the client.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Unhandled exception on {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred while processing your request",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Instance = httpContext.Request.Path,
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
```

- [x] **Step 4: Register it in `Program.cs`**

Add the using:

```csharp
using AniKo_API.Middleware;
```

After `builder.Services.AddHealthChecks();` add:

```csharp
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
```

After `var app = builder.Build();` and before `app.UseHttpsRedirection();` add:

```csharp
app.UseExceptionHandler();
```

- [x] **Step 5: Run tests to verify they pass**

```bash
cd backend && dotnet test AniKo.slnx
```

Expected: PASS, 6 tests.

- [x] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(backend): add global exception handler returning ProblemDetails"
```

---

### Task 5: Packages and layer folders

Lays down the shape from the spec so it is visible before it is full. Folders need a tracked file to survive git, so each gets a one-line `README.md` stating its single responsibility — which is more useful than an empty `.gitkeep`.

**Files:**
- Modify: `backend/AniKo_API/AniKo_API.csproj`
- Create: `backend/AniKo_API/{Data,Models,DTOs,Mappers,Validators,Repositories,Services,Configuration}/README.md`

**Interfaces:**
- Consumes: nothing
- Produces: the package references Phases C–F depend on

- [x] **Step 1: Add the packages**

```bash
cd backend/AniKo_API
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 10.0.3
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.11
dotnet add package FluentValidation.DependencyInjectionExtensions --version 12.1.1
dotnet add package Scalar.AspNetCore --version 2.16.20
```

- [x] **Step 2: Mark the Design package as a build-time-only dependency**

In `backend/AniKo_API/AniKo_API.csproj`, replace the `Microsoft.EntityFrameworkCore.Design` line with:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.11">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

Without `PrivateAssets`, the design-time tooling ships in the published container for no reason.

- [x] **Step 3: Enable XML docs**

In the first `<PropertyGroup>` of `backend/AniKo_API/AniKo_API.csproj`, add:

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<NoWarn>$(NoWarn);1591</NoWarn>
```

`1591` is suppressed because requiring a doc comment on every public member produces noise, not documentation. The XML file itself is what feeds OpenAPI descriptions in Phase F.

- [x] **Step 4: Create the layer folders**

```bash
cd backend/AniKo_API
for d in Data Models DTOs Mappers Validators Repositories Services Configuration; do
  mkdir -p "$d"
done
echo 'DbContext, migrations, and seeding. Nothing here knows about HTTP.' > Data/README.md
echo 'EF Core entities. Persistence shape only — never returned over the wire.' > Models/README.md
echo 'Wire contracts. Records, immutable, shaped for the dashboard panel that consumes them.' > DTOs/README.md
echo 'Static entity-to-DTO mapping. Pure functions; no DI, no state.' > Mappers/README.md
echo 'FluentValidation validators for every request body and query parameter.' > Validators/README.md
echo 'Data access behind interfaces. EF Core lives here and nowhere else.' > Repositories/README.md
echo 'Business logic: distance, deltas, clamping. Testable without HTTP.' > Services/README.md
echo 'Environment and connection-string resolution. Platform quirks are isolated here.' > Configuration/README.md
```

- [x] **Step 5: Verify the build is still clean**

```bash
cd backend && dotnet build AniKo.slnx && dotnet test AniKo.slnx
```

Expected: build succeeds, 6 tests pass.

- [x] **Step 6: Commit**

```bash
git add -A
git commit -m "chore(backend): add packages and layer folders"
```

---

### Task 6: Scalar API reference

A deployed API nobody can browse is half a deliverable, so Scalar is mounted in the hosted environment too, not only in Development.

**Files:**
- Modify: `backend/AniKo_API/Program.cs`
- Create: `backend/AniKo_API.Tests/OpenApiTests.cs`

**Interfaces:**
- Consumes: `ApiFactory` from Task 3
- Produces: `/openapi/v1.json` and `/scalar/v1` routes

- [x] **Step 1: Write the failing test**

Create `backend/AniKo_API.Tests/OpenApiTests.cs`:

```csharp
using System.Net;

namespace AniKo_API.Tests;

public class OpenApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public OpenApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task OpenApiDocument_IsServed()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ScalarReference_IsServed()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

- [x] **Step 2: Run to verify failure**

```bash
cd backend && dotnet test AniKo.slnx --filter OpenApiTests
```

Expected: `ScalarReference_IsServed` FAILS with 404. `OpenApiDocument_IsServed` may already pass — `MapOpenApi` is not yet called, so expect 404 there too.

- [x] **Step 3: Mount OpenAPI and Scalar**

Add the using to `backend/AniKo_API/Program.cs`:

```csharp
using Scalar.AspNetCore;
```

After `app.UseExceptionHandler();` add:

```csharp
// Mounted in every environment on purpose: a deployed API that cannot be
// browsed is half a deliverable. Revisit if this ever serves private data.
app.MapOpenApi();
app.MapScalarApiReference();
```

The no-argument overload is deliberate — see the Global Constraints note about Scalar 2.x. If a title is wanted, add it only after confirming the current options API compiles.

- [x] **Step 4: Run tests to verify they pass**

```bash
cd backend && dotnet test AniKo.slnx
```

Expected: PASS, 8 tests.

- [x] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(backend): serve OpenAPI document and Scalar reference"
```

---

# Phase B — Deploy the empty skeleton

No database, no entities, no features. The only question this phase answers is whether a .NET container built from this repo can come up on Render and go green.

---

### Task 7: Platform environment detection

Render assigns a port via `PORT` and terminates TLS at its edge. Both facts need acting on, and both are easy to get wrong in a way that only shows up in a deploy log. Isolating them in one class makes them unit-testable on a laptop.

**Files:**
- Create: `backend/AniKo_API/Configuration/PlatformEnvironment.cs`, `backend/AniKo_API.Tests/PlatformEnvironmentTests.cs`
- Modify: `backend/AniKo_API/Program.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `PlatformEnvironment.IsHosted(IConfiguration)` → `bool`; `PlatformEnvironment.GetListenUrl(IConfiguration)` → `string?`

- [x] **Step 1: Write the failing tests**

Create `backend/AniKo_API.Tests/PlatformEnvironmentTests.cs`:

```csharp
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
```

The `Theory` matters. Binding to `http://0.0.0.0:not-a-port` throws at startup with an error that reads like a Kestrel bug rather than a bad environment variable. Falling back to the default is recoverable; crashing on a malformed value is not.

- [x] **Step 2: Run to verify failure**

```bash
cd backend && dotnet test AniKo.slnx --filter PlatformEnvironmentTests
```

Expected: FAIL — `PlatformEnvironment` does not exist.

- [x] **Step 3: Write the implementation**

Create `backend/AniKo_API/Configuration/PlatformEnvironment.cs`:

```csharp
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
```

- [x] **Step 4: Run tests to verify they pass**

```bash
cd backend && dotnet test AniKo.slnx --filter PlatformEnvironmentTests
```

Expected: PASS, 9 tests in this class.

- [x] **Step 5: Wire it into `Program.cs`**

Add the using:

```csharp
using AniKo_API.Configuration;
```

Immediately after `var builder = WebApplication.CreateBuilder(args);` add:

```csharp
var listenUrl = PlatformEnvironment.GetListenUrl(builder.Configuration);
if (listenUrl is not null)
{
    builder.WebHost.UseUrls(listenUrl);
}

var isHosted = PlatformEnvironment.IsHosted(builder.Configuration);
```

Then add forwarded-headers support. After `builder.Services.AddExceptionHandler<GlobalExceptionHandler>();`:

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Render's proxy is not in a known network range; clearing these accepts it.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
```

with the using:

```csharp
using Microsoft.AspNetCore.HttpOverrides;
```

Replace `app.UseHttpsRedirection();` with:

```csharp
app.UseForwardedHeaders();

// TLS terminates at Render's edge. Redirecting inside the container sees plain
// HTTP and produces a redirect loop, so this only runs when self-hosted.
if (!isHosted)
{
    app.UseHttpsRedirection();
}
```

`UseForwardedHeaders` must come before anything that reads the scheme or client IP, which is why it is first in the pipeline.

- [x] **Step 6: Verify the whole suite still passes**

```bash
cd backend && dotnet build AniKo.slnx && dotnet test AniKo.slnx
```

Expected: build succeeds, 17 tests pass.

- [x] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(backend): bind assigned port and honour forwarded headers when hosted"
```

---

### Task 8: Dockerfile

Adapted from the sample's, with three corrections: the sample has a **duplicated `ENTRYPOINT`** line, its single-`.csproj` `COPY` breaks the moment a test project joins the solution, and its build context assumes the project sits at the repo root.

**Files:**
- Create: `Dockerfile`, `.dockerignore`

**Interfaces:**
- Consumes: `backend/AniKo.slnx` from Task 2
- Produces: an image whose entrypoint is `dotnet AniKo_API.dll`

- [x] **Step 1: Write `.dockerignore`**

Create `.dockerignore` at the repo root:

```dockerignore
**/bin/
**/obj/
**/.vs/
**/node_modules/
**/dist/
.git/
.github/
.remember/
plan/
docs/
frontend/
*.md
```

`frontend/` is excluded because the API image has no use for it. Sending it would slow every build for nothing. Phase G builds the frontend as a separate Render static site, not from this image.

- [x] **Step 2: Write the Dockerfile**

Create `Dockerfile` at the repo root:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy only the project files first so `restore` is cached independently of source changes.
COPY ["backend/AniKo.slnx", "backend/"]
COPY ["backend/AniKo_API/AniKo_API.csproj", "backend/AniKo_API/"]
COPY ["backend/AniKo_API.Tests/AniKo_API.Tests.csproj", "backend/AniKo_API.Tests/"]
RUN dotnet restore "backend/AniKo_API/AniKo_API.csproj"

COPY backend/ backend/
RUN dotnet publish "backend/AniKo_API/AniKo_API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Drop privileges. $APP_UID is provided by the base image.
USER $APP_UID

ENTRYPOINT ["dotnet", "AniKo_API.dll"]
```

There is no `EXPOSE` and no hardcoded port. Render assigns the port at runtime and Task 7 binds whatever it assigns; baking a number in here would be misleading at best.

- [x] **Step 3: Build the image**

```bash
docker build -t aniko-api:local .
```

Expected: build succeeds. If it fails on the `COPY` of the test `.csproj`, Task 2 did not create the test project at the expected path.

- [x] **Step 4: Run it the way Render will**

```bash
docker run --rm -e PORT=10000 -e RENDER=true -p 10000:10000 --name aniko-api-test aniko-api:local
```

In a second terminal:

```bash
curl -sS -o /dev/null -w "%{http_code}\n" http://localhost:10000/health
curl -sS http://localhost:10000/
```

Expected: `200`, then the info JSON. This is the exact failure mode being guarded against — if the container ignored `PORT` and bound 8080, `curl` would refuse to connect.

Then stop it with `docker stop aniko-api-test`.

- [x] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(backend): add multi-stage Dockerfile and dockerignore"
```

---

### Task 9: Render blueprint

**Files:**
- Create: `render.yaml`

**Interfaces:**
- Consumes: `Dockerfile` from Task 8, `/health` from Task 3
- Produces: a Render service named `aniko-api`

- [x] **Step 1: Write the blueprint**

Create `render.yaml` at the repo root:

```yaml
services:
  - type: web
    name: aniko-api
    runtime: docker
    plan: free
    region: singapore
    dockerfilePath: ./Dockerfile
    dockerContext: .
    healthCheckPath: /health
    autoDeploy: true
    envVars:
      - key: ASPNETCORE_ENVIRONMENT
        value: Production
```

`region: singapore` is the closest Render region to the Philippines.

No `databases:` block yet. Phase C adds Postgres; keeping it out means a failed deploy here can only be a container, port, or health-check problem.

`healthCheckPath` is what makes a failed startup fail the deploy instead of publishing a broken service. It is the entire reason startup errors are fatal rather than logged.

- [x] **Step 2: Verify the YAML parses**

```bash
python -c "import yaml,sys; yaml.safe_load(open('render.yaml')); print('ok')"
```

Expected: `ok`.

- [x] **Step 3: Commit**

```bash
git add render.yaml
git commit -m "feat: add Render blueprint for the API service"
```

---

### Task 10: Deploy, then prove the failure path works

The deploy is only half of this task. An untested failure path is an assumption, and the whole reason startup errors are fatal is that Render should refuse to publish a broken build. That claim gets verified here, once, while there is nothing else that could be causing it.

**Files:** none — this task is operational.

**Interfaces:**
- Consumes: `render.yaml` from Task 9
- Produces: a live URL, recorded in the plan checklist

- [x] **Step 1: Push the branch and open a PR to `main`**

The branch is already pushed. What remains is the PR. The base is `main`, not
`master` — `master` was retired after this plan was written.

```bash
git push -u origin aniko-backend-phase-ab
```

- [x] **Step 2: Create the Blueprint on Render**

In the Render dashboard: **New → Blueprint**, select this repository, confirm it picks up `render.yaml`, and apply. This is a manual, human step — it requires dashboard authentication and cannot be scripted here.

- [x] **Step 3: Watch the deploy**

Expected in the log, in order: Docker build succeeds → container starts → `Now listening on: http://0.0.0.0:<port>` → health check passes → **Live**.

If it hangs at "port scan timeout", `PORT` is not being honoured — return to Task 7.

- [x] **Step 4: Verify the live service**

```bash
curl -sS -o /dev/null -w "%{http_code}\n" https://aniko-api.onrender.com/health
curl -sS https://aniko-api.onrender.com/
```

Expected: `200`, then the info JSON with `"dataStore": "None (skeleton)"`.

Also open `https://aniko-api.onrender.com/scalar/v1` in a browser and confirm the reference renders.

- [x] **Step 5: Prove a broken build fails the deploy**

Temporarily break the entrypoint:

```bash
sed -i 's/AniKo_API.dll/AniKo_API_BROKEN.dll/' Dockerfile
git commit -am "test: deliberately break entrypoint to verify deploy failure"
git push
```

Expected: the container starts, immediately exits, the health check never passes, Render marks the deploy **failed**, and the **previous version keeps serving**. Confirm the last part — `curl /health` should still return 200 throughout.

- [x] **Step 6: Revert the break**

```bash
git revert --no-edit HEAD
git push
```

Expected: deploy goes green again.

- [ ] **Step 7: Record the outcome**

Tick the Phase A and Phase B boxes in `plan/backend plan/CHECKLIST.md`, including "A deliberately broken build **fails** the deploy rather than publishing". Note the live URL and the observed cold-start time — Phase H needs the number, and the frontend's loading states have to survive it.

---

## Definition of done

- `dotnet build backend/AniKo.slnx` clean, `dotnet test backend/AniKo.slnx` green with 17 tests
- `git status` clean; no `bin/`, `obj/`, `.vs/` or `.user` files tracked
- `docker build` succeeds and the container honours `PORT`
- `https://<service>.onrender.com/health` returns 200 and `/scalar/v1` renders
- A deliberately broken build has been observed failing the deploy without taking the live service down
- Phase A and Phase B fully ticked in `plan/backend plan/CHECKLIST.md`

## Explicitly not in this plan

Deferred to keep each deploy risk isolated — none of these is an oversight:

- Any database, entity, migration or seed — Phase C and D
- Any `/api/v1` endpoint — Phase F
- CORS and the frontend static site — Phase G
- Rate limiting, caching, pagination, structured logging, authentication — Phase H
