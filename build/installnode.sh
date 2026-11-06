#!/usr/bin/env bash

# Installs Node.js on a Helix agent if not already available.
# Usage: ./installnode.sh <version> <architecture>
# Based on dotnet/aspnetcore's eng/helix/content/installnode.sh

set -e

if type -P "node" &>/dev/null; then
    if node --version &>/dev/null; then
        echo "node is already in \$PATH: $(node --version)"
        exit
    else
        echo "node found on PATH but not functional, installing fresh copy"
    fi
fi

node_version=$1
arch=${2:-x64}
osname=$(uname -s)
if [ "$osname" = "Darwin" ]; then
   platformarch="darwin-$arch"
else
   platformarch="linux-$arch"
fi

echo "PlatformArch: $platformarch"
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
output_dir="$DIR/node"
url="https://nodejs.org/dist/v$node_version/node-v$node_version-$platformarch.tar.gz"
echo "Downloading Node.js $node_version from: $url"
tmp="$(mktemp -d -t install-node.XXXXXX)"

cleanup() {
    exitcode=$?
    if [ $exitcode -ne 0 ]; then
      echo "Failed to install Node.js with exit code: $exitcode"
    fi
    rm -rf "$tmp"
    exit $exitcode
}

trap "cleanup" EXIT
cd "$tmp"
curl -Lsfo "$(basename $url)" "$url" --retry 5
echo "Installing Node.js from $(basename $url)"
mkdir -p "$output_dir"
echo "Unpacking to $output_dir"
tar --strip-components 1 -xzf "node-v$node_version-$platformarch.tar.gz" --no-same-owner --directory "$output_dir"
echo "Node.js $node_version installed to $output_dir"
