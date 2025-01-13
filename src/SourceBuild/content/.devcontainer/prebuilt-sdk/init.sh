#!/usr/bin/env bash

source="${BASH_SOURCE[0]}"
script_root="$( cd -P "$( dirname "$source" )" && pwd )"

"$script_root"/../../prep-source-build.sh

cp "$script_root/../synchronize-vmr.sh" "/workspaces/"
"$script_root"/../../build.sh --online --clean-while-building || exit 0
