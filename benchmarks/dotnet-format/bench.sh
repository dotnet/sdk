#!/usr/bin/env bash
#
# Times a single `dotnet format whitespace` invocation against a generated repo.
#
# Usage:
#   bench.sh <dotnetHost> <toolDll> <repoPath> [extraArgs...]
#
# Prints the wall-clock duration in milliseconds on stdout.
set -euo pipefail

DOTNET_HOST="${1:?dotnet host required}"
TOOL_DLL="${2:?tool dll required}"
REPO_PATH="${3:?repo path required}"
shift 3

START=$(date +%s%N)
"$DOTNET_HOST" "$TOOL_DLL" whitespace --no-restore "$REPO_PATH" "$@" >/dev/null 2>&1
END=$(date +%s%N)

echo $(((END - START) / 1000000))