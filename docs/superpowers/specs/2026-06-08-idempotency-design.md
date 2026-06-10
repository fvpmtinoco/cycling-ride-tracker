# Idempotency Key — Design Spec

**Date:** 2026-06-08  
**Branch:** feat/command-handler  
**Scope:** `POST /files/fit` (opt-in via `[Idempotent]` attribute; reusable on any future POST action)

---

## Overview

Clients can attach an `Idempotency-Key` header to a POST request. The server stores the response for that key and replays it verbatim on duplicate submissions. This prevents double-processing caused by network retries.

---

## Architecture

Three new components, each with a single responsibility:

| Component | Location | Purpose |
|---|---|---|
| `IdempotencyKey` entity | `Domain/Idempotency/` | Persisted record of a key, body hash, and cached response |
| `IdempotencyFilter` + `[Idempotent]` | `Api/Filters/` | ASP.NET Core action filter; checks/stores keys, short-circuits on replay |
| EF config + migration | `Infrastructure/Idempotency/` | `idempotency_keys` table with unique index on `key` |

`IDatabaseContext` and `DatabaseContext` gain `DbSet<IdempotencyKey>`.

`[Idempotent]` implements `IFilterFactory`, resolving `IdempotencyFilter` from DI. Apply it to any action to opt in:

```csharp
[HttpPost("fit", Name = nameof(SaveFitFile))]
[Idempotent]
public async Task<IResult> SaveFitFile(...)
```

---

## Domain Model

```csharp
// Domain/Idempotency/IdempotencyKey.cs
public sealed class IdempotencyKey
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Key { get; init; }          // value of Idempotency-Key header
    public required string RequestHash { get; init; }  // SHA-256 hex of raw request body
    public required int ResponseStatusCode { get; init; }
    public required string ResponseBody { get; init; } // serialized JSON
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; } // CreatedAt + 24 h
}
```

Database table: `idempotency_keys` (snake_case via Npgsql convention).  
Unique index on `key`. No FK constraints — the record is self-contained.

---

## Data Flow

```
Client → POST /files/fit
         Idempotency-Key: <uuid>

[IdempotencyFilter — OnActionExecuting]
  1. Read Idempotency-Key header.
     → absent: skip filter entirely, proceed to action.
  2. Enable request buffering; read body bytes; compute SHA-256 hex.
  3. Reset body stream to position 0 (so model binding still works).
  4. Query: SELECT * FROM idempotency_keys WHERE key = ? AND expires_at > now()
     → not found: proceed to action (go to step 5).
     → found, hash matches: write cached status + body to response; short-circuit.
     → found, hash mismatch: return 422 (see Error Handling).

[Action executes]
  5. Normal handler logic runs; produces IResult.

[IdempotencyFilter — OnActionExecuted]
  6. Capture status code + JSON body from the response.
  7. INSERT INTO idempotency_keys (...) ON CONFLICT (key) DO NOTHING.
     (Unique constraint handles concurrent duplicate; swallow violation.)
  8. Return response to client.
```

---

## Error Handling

| Scenario | Behaviour |
|---|---|
| `Idempotency-Key` absent | Skip filter, execute normally |
| Key not found (or expired) | Execute normally, then store |
| Key found, hash matches | Replay cached response (same status code) |
| Key found, hash mismatch | `422` `{"code":"IdempotencyKey.Conflict","message":"The idempotency key was already used with a different request body."}` |
| Concurrent duplicate (race) | Unique constraint violation on insert → swallow, return response normally |
| Expired key (`expires_at < now`) | Treated as new request |

The `422` body follows the existing `CustomResults.Problem` shape used elsewhere in the project.

---

## Request Body Hashing

- Enable `EnableBuffering()` before reading so the stream can be reset.
- Read all bytes from `HttpContext.Request.Body`.
- Compute `SHA256` and encode as lowercase hex string.
- Reset `Body.Position = 0` before returning control to the pipeline.

For `multipart/form-data` (current use case), the client must send the exact same bytes on retry — including the same boundary string. This is standard behaviour for HTTP clients that buffer and replay requests.

---

## Expiry

- `ExpiresAt = CreatedAt + TimeSpan.FromHours(24)`.
- Expired records are not deleted automatically; they are simply excluded by the `WHERE expires_at > now()` clause.
- Background cleanup is out of scope for this feature.

---

## Testing (Integration)

All tests in `ListFilesEndpointTests`-style class: `IdempotencyEndpointTests`.

| # | Test | Expected |
|---|---|---|
| 1 | POST without `Idempotency-Key` | `201`, new ride created |
| 2 | Two POSTs with same key + same body | Both `201`, second returns same `Id`, no second DB row |
| 3 | Two POSTs with same key + different `rideDate` | Second returns `422` |
| 4 | POST after key expired (set `ExpiresAt` to past in DB) | `201`, new ride created |
| 5 | POST without auth token + idempotency key | `401` (auth runs before filter) |

---

## Files Changed / Created

| File | Action |
|---|---|
| `Domain/Idempotency/IdempotencyKey.cs` | Create |
| `Infrastructure/Idempotency/IdempotencyKeyConfiguration.cs` | Create |
| `Infrastructure/Database/DbContext.cs` | Add `DbSet<IdempotencyKey>` |
| `Application/Abstractions/Data/IDatabaseContext.cs` | Add `DbSet<IdempotencyKey>` |
| `Api/Filters/IdempotencyFilter.cs` | Create (filter + attribute) |
| `Api/Controllers/FilesController.cs` | Add `[Idempotent]` to `SaveFitFile` |
| `Api/Program.cs` | Register `IdempotencyFilter` in DI |
| New EF migration | Create `idempotency_keys` table |
| `Tests.Integration/IdempotencyEndpointTests.cs` | Create |
