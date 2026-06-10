#!/usr/bin/env bash
# Creates a 'current' symlink pointing to the single TFM output directory.
# Used by the VS Code launch config so the debugger finds the binary without a TFM prompt.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BIN_DIR="${1:-"$SCRIPT_DIR/../../../artifacts/bin/dotnetup/Debug"}"
BIN_DIR="$(cd "$BIN_DIR" && pwd)"

tfm_dirs=("$BIN_DIR"/net*/)

# Filter to only existing directories
existing=()
for d in "${tfm_dirs[@]}"; do
    [ -d "$d" ] && existing+=("$d")
done

if [ "${#existing[@]}" -eq 1 ]; then
    link="$BIN_DIR/current"
    rm -rf "$link"
    ln -s "${existing[0]}" "$link"
    echo "Linked current -> $(basename "${existing[0]}")"
elif [ "${#existing[@]}" -gt 1 ]; then
    names=$(printf '%s, ' "${existing[@]}" | sed 's|/,|,|g; s|.*/||g; s/, $//')
    echo "Error: Multiple TFM directories found under '$BIN_DIR': $names. Delete the stale one." >&2
    exit 1
else
    echo "Error: No TFM directory found under '$BIN_DIR'. Build first." >&2
    exit 1
fi
