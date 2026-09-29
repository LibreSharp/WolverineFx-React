# WolverineFx + React Template — Project Review

**Date reviewed:** 2026-09-29. **Commit:** `bf51662`. Based on the current source; I read the file references below directly. This review adds to the repo's own punch list in `SPEC.md` §11 rather than repeating it.

## Status: Findings 1–3 fixed in the working tree; 4–5 open

**2026-09-29:** Findings 1–3 are fixed (see Action Items below). All 4 `{marten,efcore} × {Identity=true,false}` combinations were generated with `-n MyApp`, and each builds with 0 errors. `dotnet test --list-tests` shows 2 tests without Identity and 12 with it. `dotnet ef migrations has-pending-model-changes` reports no drift for either EF Core variant. With Identity on, a Production start fails as intended when the signing key is missing or is the dev key. **None of the tests has been run**: this machine has no Docker and no local Postgres. Finding 4 (and CI, finding 5) is what will actually exercise them.

## Findings

### 1. Todo writes probably never save (correctness, high)

The codebase has no `opts.Policies.AutoApplyTransactions()` and no `[Transactional]` attribute. As far as I know, Wolverine only adds its save-changes step to an endpoint when one of those is present; it doesn't do it by default.

- **Marten:** `src/ApiHost/Program.cs` calls a bare `builder.Host.UseWolverine()`. `CreateTodo.cs` calls `session.Store(todo)` and `CompleteTodo.cs` sets `IsComplete`, but neither calls `SaveChangesAsync`.
- **EF Core:** `Data/EfCore/EfCorePersistence.cs` registers `UseEntityFrameworkCoreTransactions()`, which also only takes effect with the auto-apply policy. `db.Todos.Add(todo)` is never saved.
- Why nothing has noticed yet: `MartenRefreshTokenStore` and `EfCoreRefreshTokenStore` call `SaveChanges` themselves, so the auth flows would work while Todos fail silently.
- **Likely symptom:** `POST /api/todos` returns 201, but `GET /api/todos` stays empty.
- **Not confirmed:** Wolverine 6.16.0 isn't in the local NuGet cache, so I couldn't check its default behavior.

### 2. Todo endpoints aren't protected when Identity is on (security, high)

With `--Identity true`, only `Identity/Endpoints/MeEndpoint.cs` has `[Authorize]`. Anyone can call `/api/todos` (list, create, complete) without logging in. Todos also aren't linked to a user, so a logged-in user can see and change everyone else's. The client's `ProtectedRoute` only hides the UI and doesn't stop anyone calling the API directly.

A template that includes auth should show how to protect a feature endpoint: `[Authorize]` (or a global `RequireAuthorization` policy) plus an owner check based on the logged-in user.

### 3. The placeholder JWT signing key reaches production (security, medium)

`Jwt:SigningKey` (`REPLACE_WITH_A_SECRET_AT_LEAST_32_BYTES_LONG_FOR_DEV_ONLY`) is in the base `appsettings.json`, not `appsettings.Development.json`. A published app will start and sign tokens with a key that anyone can read. `IdentityJwtSetup` never checks for this.

### 4. Nothing has run against a real Postgres (verification gap, high)

`SPEC.md` §11 already lists this. The integration tests (`tests/ApiHost.Tests`, Alba + Testcontainers) have been listed but never run. Running them would confirm or rule out finding 1 right away.

### 5. No CI (process, medium)

`SPEC.md` §11 lists this too. Nothing regenerates and builds the 8 parameter combinations (`Persistence` × `Identity` × `IncludeTests`) automatically. GitHub-hosted Ubuntu runners have Docker, so a CI workflow would also close finding 4 for good.

## Required Fix

- Add `opts.Policies.AutoApplyTransactions()` to both persistence branches: the Marten `UseWolverine` call in `Program.cs`, and the `UseWolverine` options in `EfCorePersistence.cs`.
- When `Identity=true`, require authorization on the Todo endpoints. Also add an `OwnerId` to `Todo`, set it from the logged-in user, and filter or check by it. This changes the EF Core schema, so both `Migrations.*` sets need regenerating.
- Move the JWT signing key out of `appsettings.json` into `appsettings.Development.json` or user-secrets. Make startup fail outside Development if the key is missing, still the placeholder, or shorter than 32 bytes.
- Add a GitHub Actions matrix that generates each combination with `dotnet new wolverine-react`, builds it, and runs `dotnet test` against Testcontainers.

## Action Items

- [x] Add the auto-transaction policy (Marten + EF Core)
- [x] Add a regression test: after creating a Todo, `GET /api/todos` returns it (the existing `TodoEndpointsTests` covers this once it runs)
- [x] Protect the Todo endpoints when `Identity=true`, scope Todos to their owner, and regenerate the `Migrations.WithIdentity` migrations
- [x] Add a regression test: an anonymous `GET /api/todos` returns 401 when `Identity=true`
- [x] Add a regression test: user A can't complete user B's Todo
- [x] Add regression tests: anonymous create/complete return 401, and user B can't see user A's Todos (`TodoAuthorizationTests`)
- [x] Move the signing key out of `appsettings.json` and fail fast outside Development
- [ ] Add a CI workflow covering the 8-combination matrix, including `dotnet test`
- [ ] Run the full manual pass against live Postgres (`SPEC.md` §11, "Live runtime verification")

## Deferred (from `SPEC.md` §11, still valid)

- Switch production to pre-generated Wolverine code (`TypeLoadMode.Static` + `codegen write`) and drop `WolverineFx.RuntimeCompilation`.
- `Microsoft.OpenApi` security advisory NU1903: blocked until a patched `Microsoft.AspNetCore.OpenApi` ships; re-check periodically.
- Package ID and publishing: choose a real ID, add a `<PackageReadmeFile>`, and decide between self-publishing and pitching to JasperFx (the WolverineFx maintainers). Findings 1–3 should be fixed before any public release.
