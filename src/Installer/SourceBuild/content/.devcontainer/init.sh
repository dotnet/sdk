#!/usr/bin/env bash

set -eux

source="${BASH_SOURCE[0]}"
script_root="$( cd -P "$( dirname "$source" )" && pwd )"

workspace_dir=$(realpath "$script_root/../../")
tmp_dir=$(realpath "$workspace_dir/tmp")
vmr_dir=$(realpath "$workspace_dir/dotnet")

cp "$vmr_dir/.devcontainer/synchronize-vmr.sh" "$workspace_dir"

mkdir -p "$tmp_dir"
