#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done

REPO_ROOT="$( cd -P "$( dirname "$SOURCE" )/../" && pwd )"

STAGE0_DIR=$REPO_ROOT/.dotnet
export PATH=$STAGE0_DIR:$PATH


export DOTNET_MULTILEVEL_LOOKUP=0

export NUGET_PACKAGES=$REPO_ROOT/.nuget/packages

# Prevent environment variable get into msbuild
unset architecture
