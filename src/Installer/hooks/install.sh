#!/bin/sh
# Installs the dotnetup pre-commit hook into .git/hooks.
# Run from anywhere inside the repo.

set -e

REPO_ROOT="$(git rev-parse --show-toplevel)"
HOOK_SRC="$REPO_ROOT/src/Installer/hooks/pre-commit"
HOOK_DST="$REPO_ROOT/.git/hooks/pre-commit"

if [ -f "$HOOK_DST" ]; then
    echo "Pre-commit hook already exists at $HOOK_DST"
    echo "Appending dotnetup hook as pre-commit-dotnetup..."
    cp "$HOOK_SRC" "$REPO_ROOT/.git/hooks/pre-commit-dotnetup"
    chmod +x "$REPO_ROOT/.git/hooks/pre-commit-dotnetup"
    echo "Done. You may need to call pre-commit-dotnetup from your existing hook."
else
    cp "$HOOK_SRC" "$HOOK_DST"
    chmod +x "$HOOK_DST"
    echo "Pre-commit hook installed at $HOOK_DST"
fi
