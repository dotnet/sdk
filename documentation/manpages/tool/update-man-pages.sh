#!/usr/bin/env sh
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

set -euo pipefail

MANPAGE_TOOL_DIR=$(cd "$(dirname "$0")" || exit; pwd)

cd "$MANPAGE_TOOL_DIR"/../sdk || exit

echo "Downloading dotnet/docs main"

if command -v git > /dev/null 2>&1; then
  git clone https://github.com/dotnet/docs --single-branch --branch main --depth 1 docs-main
elif command -v curl > /dev/null 2>&1; then
  curl -sSL https://github.com/dotnet/docs/archive/main.tar.gz | tar -xvz > /dev/null
elif command -v wget > /dev/null 2>&1; then
  wget -qO- https://github.com/dotnet/docs/archive/main.tar.gz | tar -xvz > /dev/null
else
  echo "Install git, curl or wget to proceed"
  exit 1
fi

ls docs-main/docs/core/tools/dotnet*.md | while read -r line;
  do
    echo "Working on $line"
    "$MANPAGE_TOOL_DIR"/remove-metadata-and-embed-includes.py "$line"
    pandoc -s -t man -V section=1 -V header=".NET" --column=500 --filter "$MANPAGE_TOOL_DIR"/man-pandoc-filter.py "$line" -o "$(basename "${line%.md}".1)"
done

rm -rf docs-main
