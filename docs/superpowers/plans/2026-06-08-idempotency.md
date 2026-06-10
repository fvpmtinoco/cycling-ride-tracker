# Idempotency Key Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add opt-in idempotency key support to `POST /files/fit` — clients send an `Idempotency-Key` header; the server replays the cached response on duplicate submissions and rejects the same key with a different request body with 422.

**Architecture:** An `[Idempotent]` attribute implementing `IFilterFactory` resolves `IdempotencyFilter` from DI. The filter hashes the raw request body with SHA-256, queries the `idempotency_keys` Postgres table, short-circuits on replay or hash conflict, and stores the response after a new request executes. The `IdempotencyKey` entity lives in Domain; EF config and migration live in Infrastructure.

**Tech Stack:** .NET 10, ASP.NET Core `IAsyncActionFilter` + `IFilterFactory`, Entity Framework Core + Npgsql, SHA-256 (`System.Security.Cryptography`).

---

## File Map

| File | Action |
|---|---|
| `Domain/Idempotency/IdempotencyKey.cs` | Create |
| `Infrastructure/Idempotency/IdempotencyKeyConfiguration.cs` | Create |
| `Infrastructure/Database/DbContext.cs` | Add `DbSet<IdempotencyKey>` |
| `Application/Abstractions/Data/IDatabaseContext.cs` | Add `DbSet<IdempotencyKey>` |
| EF migration (auto-generated name) | Create via CLI |
| `Api/Filters/IdempotencyFilter.cs` | Create (filter + attribute) |
| `Api/Controllers/FilesController.cs` | Add `[Idempotent]` to `SaveFitFile` |
| `Api/Program.cs` | Register `IdempotencyFilter` in DI |
| `Tests.Integration/IdempotencyEndpointTests.cs` | Create |

Paths below omit the repeated prefix `src/Cycling.Rider.Tracking.Api/` for brevity. Full paths from repo root:
- Domain → `src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Domain/`
- Infrastructure → `src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Infrastructure/`
- Application → `src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Application/`
- Api → `src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api/`
- Tests → `tests/Cycling.Rider.Tracking.Tests.Integration/`

---

### Task 1: Domain Entity

**Files:**
- Create: `src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Domain/Idempotency/IdempotencyKey.cs`

- [ ] **Step 1: Create the entity**

```csharp
// src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Domain/Idempotency/IdempotencyKey.cs
namespace Cycling.Rider.Tracking.Domain.Idempotency;

public sealed class IdempotencyKey
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Key { get; init; }
    public required string RequestHash { get; init; }
    public required int ResponseStatusCode { get; init; }
    public required string ResponseBody { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; set; }  // set (not init) — tests mutate this to simulate expiry
}
```

- [ ] **Step 2: Build to verify no compilation errors**

```
dotnet build src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Domain/Cycling.Rider.Tracking.Domain.csproj -p:NuGetAudit=false
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Domain/Idempotency/IdempotencyKey.cs
git commit -m "feat: add IdempotencyKey domain entity"
```

---

### Task 2: EF Infrastructure

**Files:**
- Create: `src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Infrastructure/Idempotency/IdempotencyKeyConfiguration.cs`
- Modify: `src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Infrastructure/Database/DbContext.cs`
- Modify: `src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Application/Abstractions/Data/IDatabaseContext.cs`

- [ ] **Step 1: Create EF configuration**

```csharp
// src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Infrastructure/Idempotency/IdempotencyKeyConfiguration.cs
using Cycling.Rider.Tracking.Domain.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cycling.Rider.Tracking.Infrastructure.Idempotency;

internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.HasKey(k => k.Id);
        builder.ToTable("idempotency_keys");
        builder.HasIndex(k => k.Key).IsUnique();
        builder.Property(k => k.Key).HasMaxLength(255).IsRequired();
        builder.Property(k => k.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(k => k.ResponseBody).IsRequired();
    }
}
```

- [ ] **Step 2: Add `DbSet<IdempotencyKey>` to `DatabaseContext`**

Open `src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Infrastructure/Database/DbContext.cs`.

Add the using at the top and the property:

```csharp
using Cycling.Rider.Tracking.Application.Abstractions.Data;
using Cycling.Rider.Tracking.Domain.Idempotency;  // add this
using Cycling.Rider.Tracking.Domain.Rides;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Cycling.Rider.Tracking.Infrastructure.Database;

public sealed class DatabaseContext(DbContextOptions<DatabaseContext> options)
    : DbContext(options), IDatabaseContext
{
    public DbSet<Ride> Rides { get; set; }
    public DbSet<Domain.Outbox.TransactionFile> TransactionFiles { get; set; }
    public DbSet<IdempotencyKey> IdempotencyKeys { get; set; }  // add this

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DatabaseContext).Assembly);
        modelBuilder.AddTransactionalOutboxEntities();
        modelBuilder.HasDefaultSchema("public");
    }
}
```

- [ ] **Step 3: Add `DbSet<IdempotencyKey>` to `IDatabaseContext`**

Open `src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Application/Abstractions/Data/IDatabaseContext.cs`.

Full updated file:

```csharp
using Cycling.Rider.Tracking.Domain.Idempotency;  // add this
using Cycling.Rider.Tracking.Domain.Outbox;
using Cycling.Rider.Tracking.Domain.Rides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Cycling.Rider.Tracking.Application.Abstractions.Data;

public interface IDatabaseContext
{
    DbSet<Ride> Rides { get; set; }
    DbSet<TransactionFile> TransactionFiles { get; set; }
    DbSet<IdempotencyKey> IdempotencyKeys { get; set; }  // add this

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Build to verify no errors before migration**

```
dotnet build src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api.csproj -p:NuGetAudit=false
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 5: Add EF migration**

```
dotnet ef migrations add AddIdempotencyKeys --project src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Infrastructure --startup-project src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api
```

Expected output ends with: `Done. To undo this action, use 'ef migrations remove'`

Open the generated migration file (under `Infrastructure/Migrations/`) and verify it contains a `CreateTable` call for `idempotency_keys` with a `CreateIndex` for a unique index on `key`.

- [ ] **Step 6: Commit**

```bash
git add src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Infrastructure/Idempotency/IdempotencyKeyConfiguration.cs
git add src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Infrastructure/Database/DbContext.cs
git add src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Application/Abstractions/Data/IDatabaseContext.cs
git add src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Infrastructure/Migrations/
git commit -m "feat: add IdempotencyKey EF configuration and migration"
```

---

### Task 3: Write Failing Integration Tests

**Files:**
- Create: `tests/Cycling.Rider.Tracking.Tests.Integration/IdempotencyEndpointTests.cs`

These tests are written **before** the filter is implemented. After this task, tests 2–5 will fail (the filter does not yet exist). Test 1 will pass (no key = normal behaviour).

- [ ] **Step 1: Create the test file**

```csharp
// tests/Cycling.Rider.Tracking.Tests.Integration/IdempotencyEndpointTests.cs
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using Cycling.Rider.Tracking.Api.Controllers;
using Cycling.Rider.Tracking.Application.Files;
using Cycling.Rider.Tracking.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cycling.Rider.Tracking.Tests.Integration;

[Collection(nameof(IntegrationTestCollection))]
public sealed class IdempotencyEndpointTests(IntegrationTestWebAppFactory factory)
{
    private static readonly DateTimeOffset RideDate =
        new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveFitFile_WithoutIdempotencyKey_ExecutesNormally()
    {
        // Arrange
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);

        // Act
        var response = await PostFitFileAsync(client, RideDate, idempotencyKey: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<SaveFileResult>();
        result!.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task SaveFitFile_WithSameKeyAndSameBody_ReturnsSameIdOnRetry()
    {
        // Arrange
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        var key = Guid.NewGuid().ToString();

        // Act
        var first = await PostFitFileAsync(client, RideDate, key);
        var second = await PostFitFileAsync(client, RideDate, key);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);

        var firstResult = await first.Content.ReadFromJsonAsync<SaveFileResult>();
        var secondResult = await second.Content.ReadFromJsonAsync<SaveFileResult>();

        firstResult!.Id.Should().Be(secondResult!.Id);
    }

    [Fact]
    public async Task SaveFitFile_WithSameKeyAndDifferentBody_Returns422()
    {
        // Arrange
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        var key = Guid.NewGuid().ToString();

        var differentRideDate = RideDate.AddDays(1);

        // Act
        var first = await PostFitFileAsync(client, RideDate, key);
        var second = await PostFitFileAsync(client, differentRideDate, key);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SaveFitFile_AfterKeyExpired_CreatesNewRide()
    {
        // Arrange
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        var key = Guid.NewGuid().ToString();

        var first = await PostFitFileAsync(client, RideDate, key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstResult = await first.Content.ReadFromJsonAsync<SaveFileResult>();

        // Expire the key directly in the database
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var record = await db.IdempotencyKeys.FirstAsync(k => k.Key == key);
        record.ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        // Act
        var second = await PostFitFileAsync(client, RideDate, key);

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondResult = await second.Content.ReadFromJsonAsync<SaveFileResult>();
        secondResult!.Id.Should().NotBe(firstResult!.Id);
    }

    [Fact]
    public async Task SaveFitFile_WithoutToken_Returns401_RegardlessOfIdempotencyKey()
    {
        // Arrange
        using var client = factory.CreateClient();
        var key = Guid.NewGuid().ToString();

        // Act
        var response = await PostFitFileAsync(client, RideDate, key);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task AuthenticateAsync(HttpClient client)
    {
        var request = new AuthController.TokenRequest(
            IntegrationTestWebAppFactory.TestUsername,
            IntegrationTestWebAppFactory.TestPassword);

        var response = await client.PostAsJsonAsync("/auth/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await response.Content.ReadFromJsonAsync<AuthController.TokenResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token!.AccessToken);
    }

    private static async Task<HttpResponseMessage> PostFitFileAsync(
        HttpClient client,
        DateTimeOffset rideDate,
        string? idempotencyKey)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent([0x01, 0x02, 0x03]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "fit", "ride.fit");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/files/fit?rideDate={Uri.EscapeDataString(rideDate.ToString("O"))}")
        {
            Content = content
        };

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request);
    }
}
```

**Note:** Test 4 directly mutates `ExpiresAt` on the `IdempotencyKey` entity. This requires `ExpiresAt` to have a `set` accessor, not just `init`. Update the entity in Task 1's file: change `public required DateTimeOffset ExpiresAt { get; init; }` to `public required DateTimeOffset ExpiresAt { get; set; }`.

- [ ] **Step 2: Build the test project**

```
dotnet build tests/Cycling.Rider.Tracking.Tests.Integration/Cycling.Rider.Tracking.Tests.Integration.csproj -p:NuGetAudit=false
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Run the tests — expect failures**

```
dotnet test tests/Cycling.Rider.Tracking.Tests.Integration/Cycling.Rider.Tracking.Tests.Integration.csproj --filter "IdempotencyEndpointTests" -p:NuGetAudit=false
```

Expected: test 1 (`WithoutIdempotencyKey`) and test 5 (`WithoutToken`) pass. Tests 2, 3, 4 fail — the filter does not exist yet.

- [ ] **Step 4: Commit**

```bash
git add tests/Cycling.Rider.Tracking.Tests.Integration/IdempotencyEndpointTests.cs
git commit -m "test: add failing integration tests for idempotency key"
```

---

### Task 4: Implement IdempotencyFilter

**Files:**
- Create: `src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api/Filters/IdempotencyFilter.cs`
- Modify: `src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api/Program.cs`
- Modify: `src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api/Controllers/FilesController.cs`

- [ ] **Step 1: Create the filter and attribute**

```csharp
// src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api/Filters/IdempotencyFilter.cs
using System.Security.Cryptography;
using Cycling.Rider.Tracking.Application.Abstractions.Data;
using Cycling.Rider.Tracking.Domain.Idempotency;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Cycling.Rider.Tracking.Api.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IdempotencyFilter>();
}

public sealed class IdempotencyFilter(
    IDatabaseContext databaseContext,
    ILogger<IdempotencyFilter> logger) : IAsyncActionFilter
{
    private const string HeaderName = "Idempotency-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues)
            || string.IsNullOrWhiteSpace(keyValues))
        {
            await next();
            return;
        }

        string key = keyValues.ToString();

        context.HttpContext.Request.EnableBuffering();
        string requestHash = await ComputeRequestHashAsync(context.HttpContext.Request, context.HttpContext.RequestAborted);
        context.HttpContext.Request.Body.Position = 0;

        var existing = await databaseContext.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(
                k => k.Key == key && k.ExpiresAt > DateTimeOffset.UtcNow,
                context.HttpContext.RequestAborted);

        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
            {
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "IdempotencyKey.Conflict",
                    Detail = "The idempotency key was already used with a different request body."
                })
                { StatusCode = StatusCodes.Status422UnprocessableEntity };
                return;
            }

            context.Result = new ContentResult
            {
                Content = existing.ResponseBody,
                ContentType = "application/json",
                StatusCode = existing.ResponseStatusCode
            };
            return;
        }

        var originalBody = context.HttpContext.Response.Body;
        using var buffer = new MemoryStream();
        context.HttpContext.Response.Body = buffer;

        try
        {
            await next();

            int statusCode = context.HttpContext.Response.StatusCode;
            buffer.Position = 0;
            string responseBody;
            using (var reader = new StreamReader(buffer, leaveOpen: true))
            {
                responseBody = await reader.ReadToEndAsync(context.HttpContext.RequestAborted);
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, context.HttpContext.RequestAborted);

            try
            {
                await databaseContext.IdempotencyKeys.AddAsync(new IdempotencyKey
                {
                    Key = key,
                    RequestHash = requestHash,
                    ResponseStatusCode = statusCode,
                    ResponseBody = responseBody,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
                }, context.HttpContext.RequestAborted);

                await databaseContext.SaveChangesAsync(context.HttpContext.RequestAborted);
            }
            catch (DbUpdateException ex)
            {
                logger.LogDebug(ex, "Idempotency key {Key} already stored by a concurrent request.", key);
            }
        }
        finally
        {
            context.HttpContext.Response.Body = originalBody;
        }
    }

    private static async Task<string> ComputeRequestHashAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms, cancellationToken);
        return Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
    }
}
```

- [ ] **Step 2: Register `IdempotencyFilter` in DI**

Open `src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api/Program.cs`.

Add the using and the registration after the existing `AddScoped` lines:

```csharp
using Cycling.Rider.Tracking.Api.Extensions;
using Cycling.Rider.Tracking.Api.Filters;          // add this
using Cycling.Rider.Tracking.Application.Abstractions.Messaging;
using Cycling.Rider.Tracking.Application.Files;
using Cycling.Rider.Tracking.Infrastructure;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPresentation();
builder.Services.AddAuthenticationInternalServices(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<ICommandHandler<SaveFileCommand, SaveFileResult>, SaveFileCommandHandler>();
builder.Services.AddScoped<IValidator<SaveFileCommand>, SaveFileCommandValidator>();
builder.Services.AddScoped<IQueryHandler<ListFilesQuery, ListFilesResult>, ListFilesQueryHandler>();
builder.Services.AddScoped<IValidator<ListFilesQuery>, ListFilesQueryValidator>();
builder.Services.AddScoped<IdempotencyFilter>();    // add this
```

(Keep the rest of `Program.cs` unchanged.)

- [ ] **Step 3: Add `[Idempotent]` to `SaveFitFile`**

Open `src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api/Controllers/FilesController.cs`.

Add the using and attribute:

```csharp
using System.Diagnostics.CodeAnalysis;
using Cycling.Rider.Tracking.Api.Extensions;
using Cycling.Rider.Tracking.Api.Filters;         // add this
using Cycling.Rider.Tracking.Api.Infrastructure;
using Cycling.Rider.Tracking.Application.Abstractions.Messaging;
using Cycling.Rider.Tracking.Application.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
```

Then on the `SaveFitFile` action, add `[Idempotent]`:

```csharp
[HttpPost("fit", Name = nameof(SaveFitFile))]
[Idempotent]                                      // add this
[ProducesResponseType<SaveFileResult>(StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<IResult> SaveFitFile(
    IFormFile fit,
    DateTimeOffset rideDate,
    CancellationToken cancellationToken)
```

- [ ] **Step 4: Build**

```
dotnet build src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api.csproj -p:NuGetAudit=false
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

If you see `CS0234` or `CS1061` errors related to `FirstOrDefaultAsync`, the `Api` project needs a direct reference to `Microsoft.EntityFrameworkCore`. Add `<PackageReference Include="Microsoft.EntityFrameworkCore" />` to `Cycling.Rider.Tracking.Api.csproj` and retry.

- [ ] **Step 5: Run all idempotency tests — expect all green**

```
dotnet test tests/Cycling.Rider.Tracking.Tests.Integration/Cycling.Rider.Tracking.Tests.Integration.csproj --filter "IdempotencyEndpointTests" -p:NuGetAudit=false
```

Expected: all 5 tests pass.

- [ ] **Step 6: Run the full integration test suite to check for regressions**

```
dotnet test tests/Cycling.Rider.Tracking.Tests.Integration/Cycling.Rider.Tracking.Tests.Integration.csproj -p:NuGetAudit=false
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api/Filters/IdempotencyFilter.cs
git add src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api/Program.cs
git add src/Cycling.Rider.Tracking.Api/Cycling.Rider.Tracking.Api/Controllers/FilesController.cs
git commit -m "feat: implement IdempotencyFilter with Postgres-backed key storage"
```
