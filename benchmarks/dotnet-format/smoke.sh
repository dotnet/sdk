#!/usr/bin/env bash
# Validates the generated workload and solution, then exercises folder cache timings.
# Usage: smoke.sh <dotnetHost> <toolDll> [buildArgs...]
# For repository feeds, pass --configfile <repoRoot>/NuGet.config as build arguments.
set -euo pipefail

DOTNET_HOST="${1:?dotnet host required}"
TOOL_DLL="${2:?tool dll required}"
shift 2
SCRIPT_DIR="$(dirname "$(realpath "$0")")"
ROOT="$(mktemp -d)"
trap 'rm -rf "$ROOT"' EXIT
mkdir -p "$ROOT/tmp"
export TMPDIR="$ROOT/tmp"

bash "$SCRIPT_DIR/generate-repo.sh" "$ROOT/repo" 2 2
for source in "$ROOT"/repo/Proj*/*.cs; do
  # Both generator branches intentionally omit the final newline.
  [[ "$(tail -c 1 "$source")" == '}' ]]
done
GUID_PATTERN='[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}'
while IFS= read -r line; do
  if [[ "$line" == Project\(* ]]; then
    [[ "$line" =~ \"\{${GUID_PATTERN}\}\"$ ]]
  fi
done < "$ROOT/repo/FormatBench.sln"

# Compilation rejects incomplete sources and validates the solution configurations.
"$DOTNET_HOST" build "$ROOT/repo/FormatBench.sln" "$@"
bash "$SCRIPT_DIR/bench.sh" "$DOTNET_HOST" "$TOOL_DLL" "$ROOT/repo" --folder
[[ -n "$(find "$TMPDIR/dotnet-format" -name '*.json' -print -quit)" ]]
bash "$SCRIPT_DIR/bench.sh" "$DOTNET_HOST" "$TOOL_DLL" "$ROOT/repo" --folder
bash "$SCRIPT_DIR/bench.sh" "$DOTNET_HOST" "$TOOL_DLL" "$ROOT/repo" --folder --verify-no-changes
bash "$SCRIPT_DIR/bench.sh" "$DOTNET_HOST" "$TOOL_DLL" "$ROOT/repo" --folder --no-cache --verify-no-changes
bash "$SCRIPT_DIR/bench.sh" "$DOTNET_HOST" "$TOOL_DLL" "$ROOT/repo/FormatBench.sln" --no-restore
