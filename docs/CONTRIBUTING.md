# Contributing

Thanks for helping. Issues and pull requests are welcome; for anything big, open an issue first
so we can agree on the approach. Read the principles in [ROADMAP.md](../ROADMAP.md): free,
open-source dependencies only, few template options, and every combination tested.

## How the repository is laid out

The repository root *is* the template: `dotnet new wolverine-react` copies it, applies the
options, and renames `ApiHost` to the project's name.

| Path | What it is |
|---|---|
| `.template.config/template.json` | The options, and which files each option excludes or renames |
| `src/`, `tests/`, `deploy/`, `docker-compose.yml`, `Directory.Packages.props`, ... | The generated app's files |
| `README.app.md`, `AGENTS.app.md`, `CLAUDE.app.md`, `dependabot.app.yml`, `docs.app/` | Become `README.md`, `AGENTS.md`, `CLAUDE.md`, `.github/dependabot.yml` and `docs/` in the app |
| `README.md`, `ROADMAP.md`, `docs/`, `scripts/`, `nuget/`, `.github/` | This repository's own files, never copied into apps |
| `LibreSharp.Templates.WolverineFxReact.csproj` | Packs the template for nuget.org. It lists what goes in the package: **add new top-level files or folders of the app there**, or apps created from nuget.org won't have them (CI's "package" job catches this) |

## Options and conditional code

The options are `Persistence` (`marten`/`efcore`), `Identity` and `IncludeTests`, with the
computed symbols `IsMarten` and `IsEfCore`. Code differs between options in two ways:

- **Whole files:** exclude or rename them per option in `template.json` (for example, the Marten
  and EF Core folders, the Identity-only tests and client features, the EF Core migrations).
  Code that differs by persistence goes in a pair of files named `<Name>.Marten.cs` and
  `<Name>.EfCore.cs` (for example `Features/Todos/CreateTodoEndpoint.Marten.cs`): each app
  gets only its own, renamed to `<Name>.cs`, with no `template.json` change needed. Keep what
  both share, such as request records and validators, in `<Name>.cs` beside them
  (`CreateTodo.cs`).
- **Lines within a file:** conditional comments, which must each be on a line of their own
  (an `#if` inside a line of text is copied as-is):

| Files | Syntax |
|---|---|
| C# | `#if (Identity)` ... `#elif (IsEfCore)` ... `#else` ... `#endif` |
| JSON (`appsettings*.json`) | `//#if (Identity)` ... `//#endif` |
| Markdown, `.csproj`, `.props`, `.slnx` | `<!--#if (Identity) -->` ... `<!--#elseif (IsMarten) -->` ... `<!--#else -->` ... `<!--#endif -->` |
| YAML, `env.example` | `#if (Identity)` ... `#endif` |

The client's TypeScript has no conditionals: files that only some options need are excluded
whole, and `App.WithIdentity.tsx` replaces `App.tsx` with Identity. Because of the conditionals,
the template's own sources don't build as they are: always work through a generated app.

Adding an option doubles the combinations CI builds and tests, so new options need real demand;
features that fit every project go in without one.

## Test your change

```bash
scripts/test-combination.sh marten true      # also: marten false, efcore true, efcore false
```

It installs this folder as the template, generates a project with that combination, and runs what
CI runs: the build with warnings as errors, the API tests (Docker must be running), and the
client's build, unit tests and lint. Run every combination your change can affect; CI runs all
eight (`IncludeTests` true and false).

The end-to-end tests run against a published app with PostgreSQL and Mailpit, as the
"Publish and run" and "End-to-end tests" steps in `.github/workflows/ci.yml` do. Locally, start
PostgreSQL and Mailpit, publish and run the generated app with the same environment variables
as that step, then run `npm ci && npx playwright test` in its `tests/e2e`. Or run them against
the Development setup, as the generated app's `docs/testing.md` describes.

Also check, when relevant:

- **Generated docs:** generate an app and read its `README.md` and `docs/`: no template markers
  left, and links resolve.
- **The package:** `dotnet pack LibreSharp.Templates.WolverineFxReact.csproj -o /tmp/pack`,
  then install it with `dotnet new install /tmp/pack/*.nupkg` and generate from it.

## Regenerate the client's API types

`src/Client/src/lib/api/schema.d.ts` is one file for every combination, generated from a Marten
app with Identity (apps without Identity simply don't call those endpoints). After changing an
endpoint's request or response:

1. Generate a Marten + Identity app named `ApiHost` (so names match the template):
   `dotnet new wolverine-react -n ApiHost -o /tmp/schema-app --Identity true`.
2. Start PostgreSQL, then run its API on port 5133 in Development.
3. In this repository's `src/Client`: `npx openapi-typescript http://localhost:5133/openapi/v1.json -o src/lib/api/schema.d.ts`.

## EF Core migrations

The EF Core option has two migration folders, chosen by `Identity`:
`src/ApiHost/Data/EfCore/Migrations.WithIdentity/` and `Migrations.NoIdentity/`. Each is renamed to
`Migrations/` on generation, file by file, in `template.json`. To add a migration:

1. Generate an EF Core app named `ApiHost` with the matching `--Identity`, and make the model
   change there.
2. Run `dotnet ef migrations add <Name> --project src/ApiHost --output-dir Data/EfCore/Migrations`
   in it, and check `dotnet ef migrations has-pending-model-changes --project src/ApiHost`.
3. Copy the new migration files and the updated `AppDbContextModelSnapshot.cs` back into the
   matching folder here, and add each new file to that folder's rename list in `template.json`.

## Things that have bitten us

- **Marten's event source generator** treats any class with `Create` or `Apply` methods as an
  event-sourced aggregate, and generates code that doesn't compile. Use other method names.
- **The analyzer** (Meziantou) runs on every build; generated apps must build with 0 warnings.
- **Tokens are in memory:** in end-to-end tests, `page.goto` reloads the app and signs out; after
  logging in, navigate with the app's links.
- **New dependencies** must be free and open source: check the licence on the package page before
  adding one, and add the version to `Directory.Packages.props` (NuGet) or the right
  `package.json`.

## Pull requests

- One topic per pull request, with tests for new behaviour.
- Update the generated app's docs (`README.app.md`, `docs.app/`, `AGENTS.app.md`) and, for
  features, this repository's `README.md` and `ROADMAP.md`.
- CI must pass for every combination.
