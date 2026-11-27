# Releasing

Push a version tag; `.github/workflows/release.yml` runs the full CI workflow (every combination,
and the package check), then packs the template with that version, pushes it to nuget.org and
creates a GitHub release:

```bash
git tag v0.3.0 && git push origin v0.3.0     # a tag with a hyphen (v0.4.0-beta.1) is a prerelease
```

Before tagging:

- The roadmap's items for the version are done, and `ROADMAP.md` says so.
- `README.md` and `nuget/README.md` describe what the version adds (nuget.org shows the latter).
- CI is green on `main`.
- `docs/releases/<tag>.md` (such as `docs/releases/v0.4.0.md`) holds the release notes, with any
  breaking changes first: the GitHub release uses it. Without one, GitHub generates notes from
  pull requests, which say little when commits go straight to `main`.

## One-time setup

So the workflow can publish without a stored API key (nuget.org Trusted Publishing):

1. On nuget.org, under your account's **Trusted Publishing** settings, add a GitHub Actions
   policy for owner `LibreSharp`, repository `WolverineFx-React`, workflow file `release.yml`.
2. In this GitHub repository, add the **variable** (not secret) `NUGET_USER`, set to that
   nuget.org account's username.

## After releasing

- Check the package page on nuget.org, then try it:
  `dotnet new install LibreSharp.Templates.WolverineFxReact@<version>` and generate a project.
- Update the template's entry on the [LibreSharp site](https://libresharp.github.io/templates/)
  if what it offers changed.
