#!/usr/bin/env bash

set -ex

source="${BASH_SOURCE[0]}"
script_root="$( cd -P "$( dirname "$source" )" && pwd )"

sdk_dir=$(realpath "$script_root/../..")
workspace_dir=$(realpath "$sdk_dir/../")
tmp_dir=$(realpath "$workspace_dir/tmp")
vmr_dir=$(realpath "$workspace_dir/dotnet")

cp "$sdk_dir/.devcontainer/vmr/synchronize-vmr.sh" "$workspace_dir"

mkdir -p "$tmp_dir"

# Codespaces performs a shallow fetch only
git -C "$sdk_dir" fetch --all --unshallow

# We will try to figure out, which branch is the current (PR) branch based off of
# We need this to figure out, which VMR branch to use
vmr_branch=$(git -C "$sdk_dir" log --pretty=format:'%D' HEAD^ \
  | grep 'origin/'    \
  | head -n1          \
  | sed 's@origin/@@' \
  | sed 's@,.*@@')

"$workspace_dir/synchronize-vmr.sh" --branch "$vmr_branch" --ci --debug

cd "$vmr_dir"
