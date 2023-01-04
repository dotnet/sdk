#!/usr/bin/env bash

source="${BASH_SOURCE[0]}"
scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

"$scriptroot"/../../prep.sh

# GitHub Codespaces sets this and it conflicts with source-build scripts.
unset RepositoryName

"$scriptroot"/../../build.sh --online --clean-while-building || exit 0
