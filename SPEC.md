# WolverineFx + React `dotnet new` Template — Planning Spec

> **Status: built and verified.** The template is complete — see README.md for usage. This
> document is now the design/decision history; §10 below records what shipped and what's still
> open.

## 1. Goal

Ship an installable `dotnet new` template that scaffolds a single-repo, single-deployable
application pairing a **WolverineFx** backend (HTTP endpoints + message bus, "Critter Stack"
style) with a **React (Vite + TypeScript)** frontend, distributed as a NuGet template package.

Inspiration:
- **react-templates.net** — variant selection at scaffold time (framework flavor, styling,
  feature set), shared type contracts between client and server, Tailwind by default.
- **wolverinefx.net/guide/samples** — no existing sample pairs Wolverine with a SPA frontend;
  this template is new ground for that ecosystem, so it should follow Wolverine's own
  conventions (Marten/EF Core persistence, HTTP endpoints via `WolverineFx.Http`) rather than
  invent new patterns.

## 2. Decisions locked in

| Decision | Choice | Rationale |
|---|---|---|
| Persistence | **Toggle**: Marten (Postgres, document/event store) or EF Core (relational) | Matches Wolverine's own sample split; avoids picking a side that undersells either the "Critter Stack" story or the familiar relational path. Accepted tradeoff: two code paths to maintain and test. |
| Frontend/backend wiring | **Single deployable**: Vite dev server proxied through Kestrel in dev (`npm run dev` + `ASPNETCORE`'s SPA proxy or a `vite dev` reverse-proxy middleware), `dist/` served as static files by Kestrel in production | No CORS to configure, one process to deploy, matches the convention ASP.NET Core's own SPA templates use. |
| Optional feature surface (v1) | **Lean**: Identity toggle + persistence toggle only | Keep the parameter matrix small until there's real usage feedback; broader toggles (Docker Compose, SignalR demo, CI workflow) are explicitly deferred, not designed for yet. |
| .NET target | **.NET 11** (originally .NET 10; moved 2026-11-26) | .NET 11 (STS) and .NET 10 (LTS) both reach end of support on 2028-11-14 now that STS gets 24 months, so the newest release costs nothing in support window. It also clears the NU1903 advisory (see §11). |
| EF Core database | **PostgreSQL** (same engine as Marten) | One Docker Compose / connection-string story for local dev regardless of which persistence option is picked. |
| Frontend styling | **Tailwind CSS** | Matches react-templates.net's default across all variants; speeds up scaffolding auth pages. |
| API typing | **OpenAPI-generated TS client** (e.g. `openapi-typescript`) | Real type-safety guarantee, matches the "full type safety" pitch; accepted cost of a codegen step in the dev workflow. |

## 3. Template identity

- **Short name** (`dotnet new <short-name>`): TBD — candidate `wolverine-react` or `wolverinereact`.
- **NuGet package id**: TBD — candidate `WolverineFx.Templates.React`. Needs to not collide with
  existing packages under the `WolverineFx.*` namespace; JasperFx (Wolverine's maintainer) owns
  that prefix, so a distinct id (e.g. `YourName.Templates.WolverineReact`) is safer unless this is
  intended to be contributed upstream to JasperFx/Wolverine itself.
- **Author intent to clarify**: is this meant to live in *your* NuGet namespace as an independent
  community template, or as a PR into Wolverine's own samples/templates repo? Affects package id,
  README framing, and where "submit to the .NET directory" actually points (see §7).

## 4. Solution layout

```
WolverineFx-React.sln
/src
  /ApiHost                  (Wolverine host: HTTP endpoints, message handlers, Program.cs — sourceName
                             for templating; the whole token is replaced by `-n` in generated instances,
                             e.g. `-n MyApp` yields `src/MyApp`, not `src/MyAppHost`)
    /Data
      /Marten                (present only if --persistence=marten)
      /EfCore                (present only if --persistence=efcore)
    /Features                (vertical-slice folders: one per Wolverine endpoint/handler group)
    /Identity                (present only if --identity=true; ASP.NET Core Identity setup, endpoints)
    Program.cs
    appsettings.json
  /Client                   (Vite + React + TypeScript SPA)
    /src
      /features
      /lib/api               (generated/shared API client, typed against server DTOs)
    vite.config.ts
/tests
  /ApiHost.Tests             (WolverineFx.Http + handler tests, Alba/xUnit — see §11)
  /Client.Tests               (Vitest + React Testing Library — not built, see §11)
.template.config/
  template.json
  ide.host.json (optional, for IDE gallery metadata)
```

## 5. Template parameters (symbols)

| Symbol | Type | Values | Effect |
|---|---|---|---|
| `Persistence` | choice | `marten` \| `efcore` | Includes/excludes `/Data/Marten` or `/Data/EfCore`; swaps `Program.cs` service registration block via `#if MARTEN` / `#if EFCORE`; swaps connection-string shape in `appsettings.json`. |
| `Identity` | bool | `true` \| `false` | Includes/excludes `/Api/Identity` and `/Client/src/features/auth`; adds ASP.NET Core Identity package refs and Wolverine HTTP auth endpoints when true; gates `app.UseAuthentication()/UseAuthorization()` calls. |
| `IncludeTests` | bool | default `true` | Includes/excludes `/tests`. Standard convention across most Microsoft templates — cheap to support, don't over-think this one. |

Mechanics: `template.json` `symbols` block with computed conditions, `sources[].exclude` for
whole-folder gating, `#if SYMBOL` preprocessor blocks (via `SymbolInfo` of type `Parameter` +
`replaces`) for in-file gating (e.g., the three lines in `Program.cs` that differ per persistence
choice, or the auth middleware registration).

Validate early: build the Marten and EF Core branches as two *actual* working projects first,
diff them, and only then decide which lines actually need `#if` gating vs. full file exclusion —
guessing this up front tends to under- or over-fragment the conditionals.

## 6. What "Identity" concretely adds

- Server: ASP.NET Core Identity (EF Core-backed `IdentityDbContext`, or Marten-backed identity
  store if one exists/is maintained — **needs a spike**, since Identity's default persistence
  model assumes EF Core; using it under a Marten-selected project may require either a second,
  small EF Core context just for Identity, or a community Marten identity store package).
- Wolverine HTTP endpoints for register/login/logout/refresh (thin wrappers, not a custom auth
  scheme).
- Client: `/features/auth` — login/register forms, an auth context/hook, route guards.

**Resolved (spike complete)**: `Marten.AspNetIdentity` (nuget.org, author mrshrinkray) is a
maintained `IUserStore`/`IRoleStore` adapter for Marten, versioned to track Marten's own major
version (currently at `9.0.26`, matching our `Marten 9.12.0`; last published 2026-05-21). Decision:
use `Marten.AspNetIdentity` when `Persistence=marten`, and standard
`Microsoft.AspNetCore.Identity.EntityFrameworkCore` against the existing `AppDbContext` when
`Persistence=efcore`. No secondary database/context needed in either branch.

## 7. Distribution / "submitting to the .NET directory"

There isn't a single Microsoft-curated directory that reviews and lists templates the way an app
store does. In practice, "getting a template into the .NET ecosystem" means:

1. Pack the template project into a NuGet package (`dotnet pack`) with a valid
   `.template.config/template.json`.
2. Publish to nuget.org — this alone makes it installable via
   `dotnet new install <PackageId>` and discoverable via `dotnet new search <keyword>` /
   nuget.org search once tagged `dotnet-new` template category tags in the `.nuspec`.
3. Optionally submit a PR to community-maintained template indexes (e.g., the `dotnet/templating`
   wiki's "Available templates" list) once the package is published and stable.
4. If the intent is specifically to get this associated with *Wolverine* (not just "a" template),
   that likely means opening an issue/PR against JasperFx's Wolverine repo proposing it as an
   official/linked sample or template — a different, higher-bar path than self-publishing.

Needs a decision from you: self-publish under your own NuGet id now, and pursue upstreaming to
Wolverine later once it's proven — or start the upstreaming conversation with JasperFx first, in
case they'd rather it live under their org's naming/versioning from day one.

## 8. Milestones

1. Build both persistence variants as standalone, non-templated projects (Marten and EF Core),
   confirm both actually work end-to-end with the Vite proxy setup.
2. Spike the Identity + Marten interaction (§6) — resolve before templating, not after.
3. Templatize: extract `template.json`, add symbols, convert hard-coded differences to `#if`
   blocks / excluded paths.
4. `dotnet new install ./src --force` locally, generate all 4 parameter combinations
   (`marten`/`efcore` × `identity=true/false`), confirm each builds and runs.
5. Pack + publish to nuget.org under a chosen package id.
6. Write README (install command, parameter table, "what you get" walkthrough).
7. Decide on upstream/community listing per §7.

## 8a. Build findings (Marten variant, built and verified)

- **Packages**: `WolverineFx`, `WolverineFx.Http`, `WolverineFx.Marten`, `Marten`, `Marten.AspNetCore` (all 6.16.0 / 9.12.0 as of this build). `.IntegrateWithWolverine()` on `AddMarten(...)` is **required**, not optional — without it, `IDocumentSession`/`IQuerySession` are opaque DI factories and Wolverine's HTTP codegen throws `InvalidServiceLocationException` at request time.
- **`WolverineFx.RuntimeCompilation`** is a separate package as of Wolverine 6.x (no longer bundled in core). Required for `TypeLoadMode.Dynamic` (the dev-time default). For production, Wolverine recommends pre-generating code (`dotnet run -- codegen write` + `TypeLoadMode.Static`) to drop the Roslyn dependency and cut startup time — **not yet implemented**, currently shipping with runtime compilation in both dev and prod. Follow-up before calling this template production-ready.
- **Client stack confirmed working**: Vite + React 19 + TS 6.0.3 + Tailwind v4 (via `@tailwindcss/vite`) + `openapi-fetch`/`openapi-typescript`. `openapi-typescript@7.13.0`'s peer range doesn't yet include TS 6, so `src/Client/.npmrc` sets `legacy-peer-deps=true` — functionally fine (verified: codegen ran correctly against the live OpenAPI spec), just a peer-range lag, not a real incompatibility.
- **Dev wiring verified**: `vite.config.ts` proxies `/api/*` to `http://localhost:5133` (was `5299` until 2026-11-26, which did not match the API port in `launchSettings.json`, so every proxied call failed) (no CORS needed, single-origin from the browser's perspective). Confirmed via a live 502 from the Vite proxy when the API wasn't running — proxy target and path are correct.
- **Prod wiring verified**: `ApiHost.csproj` has a `PublishClient` target (`AfterTargets="ComputeFilesToPublish"`) that runs `npm install && npm run build` in `src/Client` and copies `dist/**` into the published `wwwroot`. Confirmed via a real `dotnet publish -c Release` that `wwwroot/index.html` and hashed asset files land correctly in the publish output.
- **Runtime CRUD not yet verified against a live database** — no Docker available in the build environment. Verified instead: (a) app reaches Marten/Wolverine storage migration and fails only on Postgres connection-refused (proves DI/registration wiring is correct), and (b) with `.IntegrateWithWolverine()` temporarily removed, captured the real OpenAPI spec and generated working TS types from it, matching the three endpoints (`GET /api/todos`, `POST /api/todos`, `POST /api/todos/{id}/complete`). A full CRUD pass against a live Postgres (e.g. via `docker compose up`) is still outstanding.
- **`docker-compose.yml`** added at repo root (Postgres 16) — this isn't a template toggle, it's baseline dev infra since both persistence options now target Postgres (§2 EF Core decision).

## 9. Open questions for you

- Package id / short name (§3) — currently packaged as placeholder `WolverineFxReact.Templates`;
  pick a real id before publishing, hard to rename cleanly later.
- Self-publish vs. pursue upstreaming into Wolverine's own repo (§7) — changes scope and pace.

## 10. Final state (what actually shipped)

All decisions in this document were carried through to a working, verified template:

- **Both persistence variants** live in the real repo under `src/ApiHost/Data/Marten/` and
  `src/ApiHost/Data/EfCore/`, gated by a `Persistence` choice symbol (`marten`/`efcore`) with computed
  booleans `IsMarten`/`IsEfCore` used for in-file `#if` gating (raw C# `#if` only supports plain
  symbols, not the string-equality expressions `template.json` itself supports for folder
  exclusion — this distinction cost a round of debugging, worth knowing if extending this template).
- **Identity spike resolved**: `Marten.AspNetIdentity` requires `TUser : IdentityUser, IClaimsUser`
  (confirmed via reflection against the package DLL, not docs) — `ApplicationUser.cs` gates the
  `IClaimsUser` implementation behind `#if (IsMarten)` so the EF Core branch doesn't need to
  reference the Marten identity package at all.
- **JWT design shipped as originally planned**: HS256 self-contained access tokens (15 min) +
  rotating refresh tokens (14 days) with reuse-detection, per-persistence refresh-token stores
  behind a shared `IRefreshTokenStore` abstraction, in-memory-only client-side token storage, and
  an `openapi-fetch` middleware that attaches the bearer header and does a single-flight
  refresh-and-retry on 401. Endpoints (`register`/`login`/`refresh`/`logout`/`me`) use ASP.NET
  Core's typed `Results<T1,T2>` union return types rather than bare `IResult` — the latter erases
  the response shape from the generated OpenAPI spec, which was caught by inspecting the actual
  generated TypeScript types, not by code review.
- **Template mechanics that needed real debugging, not just the schema docs**: nested
  persistence-specific folders under `Identity/Marten/` and `Identity/EfCore/` needed their own
  exclusion rules (easy to miss when only gating the top-level `Identity/` folder on the `Identity`
  symbol); the template source directory itself needed explicit `node_modules`/`dist`/`bin`/`obj`
  exclusion in `template.json`, since `dotnet new` copies the literal template source tree
  verbatim and a stray `node_modules` in the source produced broken partial copies in every
  generated instance; the `App.tsx` Identity-vs-not difference is handled via a
  duplicate-file-plus-`rename` pair (`App.WithIdentity.tsx` → `App.tsx`) rather than in-file `#if`,
  since `.tsx` conditional-comment support wasn't verified and this sidesteps the question
  entirely.
- **Verified**: all 8 parameter combinations (`{marten,efcore} × {true,false} × {true,false}`)
  build cleanly, both as a loose-directory template install and as a packed `.nupkg` installed
  fresh (`dotnet pack` → `dotnet new install *.nupkg` → generate → build). `dotnet publish` on the
  default combination produces a working `wwwroot` with the built client assets.
- **Not verified**: live CRUD and login/refresh against a running Postgres (no Docker in the build
  environment, per user's explicit call to proceed on wiring-level proof only — see §8a).

## 11. Remaining work

Grouped by how much it blocks calling this "done."

### Blocking before real use

- ~~`sourceName: "Api"` collides with substrings of unrelated identifiers~~ — **fixed 2026-07-20**:
  renamed the source project (folder, `.csproj`, namespaces) from `Api` to `ApiHost` everywhere —
  `src/Api` → `src/ApiHost`, `Api.csproj` → `ApiHost.csproj`, `namespace Api.X` → `namespace
  ApiHost.X`, `.template.config/template.json`'s `sourceName` and its path-based modifiers, and
  `WolverineFxReact.slnx`. `"ApiHost"` isn't a substring of `"OpenApi"` or any other package/type
  name in the template, so the collision is gone. Verified by generating all 4
  `{marten,efcore} × {Identity=true,false}` combinations with `-n MyApp` (a name that previously
  triggered the bug) and confirming every one builds with 0 errors and `OpenApi`-related types are
  untouched. Note for anyone extending this template: because `dotnet new`'s `sourceName`
  replacement swaps the *entire* token for `-n`'s value, `src/ApiHost` becomes `src/MyApp` in a
  generated instance (not `src/MyAppHost`) — this was already true before the fix (with `Api` ->
  `MyApp`), so no behavior change for end users, just no more collision.
- **Integration tests run against real Postgres — 2026-11-26.** First run (Docker + Testcontainers,
  .NET 11) of all 4 test-bearing combinations: 28/28 pass after fixing three bugs the run exposed:
  (1) with Identity, `AddIdentity` made Identity's cookie scheme the default, so unauthenticated API
  calls got a 302 login redirect instead of 401 — `IdentityJwtSetup` now sets JWT as the default
  authenticate/challenge scheme after `AddIdentity`; (2) with Identity, every endpoint taking
  `UserManager<ApplicationUser>` returned 500 because its constructor takes `IServiceProvider` and
  Wolverine's default `ServiceLocationPolicy.NotAllowed` rejects that — fixed with
  `AlwaysUseServiceLocationFor<UserManager<ApplicationUser>>()`; (3) Marten + Identity login/refresh
  returned 500 because `Marten.AspNetIdentity`'s store has no `IUserRoleStore` (roles live on
  `IClaimsUser.RoleClaims`) — added `Identity/UserRoles.cs` (`GetUserRolesAsync`). Also corrected
  tests that expected 200 from create (201) and complete (204). The manual end-to-end pass below
  (browser + `docker compose`) is still outstanding.
- **Live runtime verification.** Everything so far proves wiring (DI resolves, codegen succeeds,
  builds pass) but nothing has actually hit a real Postgres. Run `docker compose up -d`, then for
  each of the 8 parameter combinations: register a user (if `Identity=true`), log in, create/list/
  complete a Todo, refresh the access token, log out, and confirm a revoked/reused refresh token
  is rejected. This is the single biggest gap — nothing below matters if this turns up a bug.
- **Package id + publish.** Currently packaged under the placeholder id `WolverineFxReact.Templates`
  (see README.md and §9). Pick a real id, then `dotnet pack` + push to nuget.org. The pack step
  also warned the package is missing its own NuGet-level readme (distinct from the repo's
  `README.md` — NuGet renders a `<PackageReadmeFile>` on the package listing page); add one if
  publishing publicly.
- **Decide self-publish vs. upstreaming into WolverineFx's own repo** (§7). Affects the package id
  above and whether this should be pitched to JasperFx before or after a public release.

### Should do before calling it production-ready

- **Switch Wolverine off dynamic codegen for production.** Right now `WolverineFx.RuntimeCompilation`
  (Roslyn) is referenced and used in both dev and prod. Wolverine's own guidance is to pre-generate
  code (`dotnet run -- codegen write`) and set `TypeLoadMode.Static` for production, dropping the
  Roslyn dependency and cutting startup time. Not yet done in either persistence branch.
- ~~**`Microsoft.OpenApi` NU1903 advisory**~~ — **fixed 2026-11-26** by moving to .NET 11:
  `Microsoft.AspNetCore.OpenApi 11.0.0` depends on the patched `Microsoft.OpenApi 3.11.x`. All 8
  combinations build with 0 warnings. Same upgrade: WolverineFx 6.65.1, Marten 9.63.0,
  Marten.AspNetIdentity 11.0.1, Npgsql EF 11.0.0, Testcontainers 4.17.0. EF Core variants pin
  `Microsoft.CodeAnalysis`/`.Scripting` 5.9.0 to reconcile EF Design 11's Roslyn with
  `WolverineFx.RuntimeCompilation`'s (drop the pin with runtime compilation). `dotnet ef migrations
  has-pending-model-changes` (EF 11) reports no drift for either migration set. History:
  `Microsoft.AspNetCore.OpenApi 10.0.9` (latest stable as of the original check) pulls in the
  vulnerable `Microsoft.OpenApi 2.0.0` transitively. Tried pinning `Microsoft.OpenApi` directly to
  the patched `3.8.0`: this breaks the build — `Microsoft.AspNetCore.OpenApi 10.0.9`'s XML-comment
  source generator targets the 2.x API shape (`IOpenApiMediaType.Example` is writable in 2.0.0,
  read-only in 3.8.0, a breaking change between OpenApi major versions), so it fails with `CS0200`
  at compile time.
  **Re-checked 2026-07-20** against the then-current `Microsoft.AspNetCore.OpenApi 10.0.10` +
  `Microsoft.OpenApi 3.9.0`: same `CS0200` failure, still unresolved (confirmed via a standalone
  probe project, not just re-reading the advisory). No `11.x` stable line has shipped yet either.
  No action to take until Microsoft ships a stable `Microsoft.AspNetCore.OpenApi` release built
  against a patched `Microsoft.OpenApi`; re-check again periodically.
- ~~Build out the `tests/` project~~ — **fixed 2026-07-20**: added `tests/ApiHost.Tests`
  (xUnit + Alba + `Testcontainers.PostgreSql`), persistence-agnostic (hits the app over HTTP, so
  one project covers both `marten`/`efcore` without `#if`). `AppFixture` spins up a real Postgres
  container per test collection and boots the app via `AlbaHost.For<Program>` (`Program.cs` needed
  a trailing `public partial class Program;` for `WebApplicationFactory`/Alba to reference the
  entry point). `TodoEndpointsTests` covers create/list/complete and a 404 case; `AuthEndpointsTests`
  (excluded via `template.json` when `Identity=false`, same mechanism as the App.tsx swap) covers
  register/login/wrong-password/the `[Authorize]` `/me` endpoint/refresh-rotation/reuse-detection/
  logout. Verified: all 4 `{marten,efcore} × {Identity=true,false}` combinations build clean and
  `dotnet test --list-tests` discovers exactly the expected tests per combination (Auth tests only
  appear when `Identity=true`). **Not verified**: actually running the tests — no Docker in this
  environment either (same constraint as §8a), so `Testcontainers.PostgreSql` can't start a
  container here. The Todo-creation assertions deliberately avoid depending on the exact
  status/headers Wolverine's `CreationResponse`/`IHttpAware` sets (couldn't pin that down without a
  live run) and instead verify behavior by title lookup — lower risk if that assumption were wrong,
  but still unexecuted code. Run `dotnet test` from `tests/ApiHost.Tests` somewhere with Docker
  before trusting it.
  **Bug found and fixed along the way**: the EF Core variant had no `Migrations/` folder and
  nothing called `Database.Migrate()`/`EnsureCreated()` — so, contrary to what "wiring verified"
  in §8a implied, a fresh EF Core instance would have failed on the very first query (`relation
  "Todos" does not exist"`) even with a reachable Postgres. Generated real migrations
  (`dotnet ef migrations add InitialCreate`) for both `Identity=true` and `Identity=false` (the
  schemas differ — Identity tables + `RefreshTokens`), checked them into
  `src/ApiHost/Data/EfCore/Migrations.{NoIdentity,WithIdentity}/`, and added `template.json`
  modifiers that exclude the unused variant and rename the other to `Migrations/` at generation
  time. `Program.cs` now calls `Database.Migrate()` on startup for the EF Core branch (`#if
  (IsEfCore)`), mirroring Marten's own auto-create-on-first-use behavior. This was necessary for
  the EF Core tests to have any chance of passing, not optional scope creep — but it's still only
  wiring-verified (migration C# compiles and the `CreateIndex`/`CreateTable` calls look right), not
  run against a live Postgres, for the same Docker-availability reason as everything else here.
- **No CI.** Nothing automatically re-runs the 8-combination build matrix on changes. Worth a
  GitHub Actions workflow (or equivalent) that regenerates and builds all 8 combinations, since
  that's exactly the kind of regression that's easy to introduce silently (e.g. the nested
  `Identity/Marten` vs `Identity/EfCore` exclusion bug found during this build would have been
  caught immediately by such a matrix).

### Documented but intentionally deferred (not a defect, just scoped out)

- **RS256/JWKS signing.** Shipped with HS256 (shared secret) as decided; upgrading to asymmetric
  signing + a `/.well-known/jwks.json` endpoint is only needed if an independent service must
  verify tokens without sharing the signing secret. Documented in README.md's "Known limitations."
- **`sessionStorage` token persistence.** Shipped as in-memory-only (safer against XSS, but tokens
  don't survive a page reload). The `sessionStorage` alternative is documented as a one-line swap
  in README.md, not built as a toggle.
- **Broader template surface** (Docker Compose toggle, SignalR demo, CI workflow toggle) — explicitly
  out of scope for v1 per §2's "lean" decision. Revisit only after real usage feedback.

### Minor / cosmetic

- ~~`src/Client/README.md` is still Vite's default scaffold readme~~ — **fixed**: replaced with a
  project-specific readme (dev commands, script table, API-client regeneration note).
- ~~`MartenRefreshTokenStore`/`EfCoreRefreshTokenStore` query refresh tokens by `TokenHash` with no
  explicit index~~ — **fixed**: added a unique index on `TokenHash` (plus a non-unique index on
  `UserId`, used by `RevokeAllForUserAsync`) to both `AppDbContext.OnModelCreating` (EF Core, `#if
  (Identity)`) and `MartenPersistence`'s `opts.Schema.For<RefreshToken>()` (Marten, `#if
  (Identity)`). Verified by generating both `Identity=true` variants (`marten`/`efcore`) via
  `dotnet new install` + `dotnet new wolverine-react` and building each — both compile clean.
  (Originally landed before EF Core had any migrations at all; now baked into the
  `Migrations.WithIdentity` initial migration added under "Build out the `tests/` project" above —
  see `IX_RefreshTokens_TokenHash` / `IX_RefreshTokens_UserId` there.)
- ~~The repo itself is not yet a git repository~~ — **fixed**: repo is now version-controlled and
  pushed to `origin/main`.
