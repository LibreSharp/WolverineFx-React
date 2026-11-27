#!/usr/bin/env bash
# Generates one combination of the template from this folder and checks it the way CI does:
# build (warnings are errors), the API tests, and the client's build, unit tests and lint.
#
#   scripts/test-combination.sh <marten|efcore> <none|identity|saas>   [output folder]
#
# Needs Docker running (the API tests start PostgreSQL). Installs this folder as the
# wolverine-react template (replacing any installed version); `dotnet new uninstall ./` removes it.
set -euo pipefail

persistence=${1:?usage: scripts/test-combination.sh <marten|efcore> <none|identity|saas> [output folder]}
auth=${2:?usage: scripts/test-combination.sh <marten|efcore> <none|identity|saas> [output folder]}
repo=$(cd "$(dirname "$0")/.." && pwd)
out=${3:-${TMPDIR:-/tmp}/wolverine-react-$persistence-$auth}

dotnet new install "$repo" --force > /dev/null
rm -rf "$out"
dotnet new wolverine-react -n MyApp -o "$out" --Persistence "$persistence" --Auth "$auth" --IncludeTests true > /dev/null
echo "Generated $out"

cd "$out"
dotnet build MyApp.slnx -warnaserror
dotnet test MyApp.slnx --no-build

cd src/Client
npm ci
npm run build
npm test
npm run lint

echo "OK: $persistence, auth $auth. End-to-end tests: see docs/CONTRIBUTING.md."
