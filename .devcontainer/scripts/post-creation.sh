#! /usr/bin/env sh

# Install SDK and tool dependencies before container starts
# Also run the full restore on the repo so that go-to definition
# and other language features will be available in C# files
./restore.sh
# run the build so that everything is 'hot'
./build.sh -tl:off
# setup the IDE env to point to the local .dotnet folder so that assemblies/tools are loaded as expected
# this script is run from repo root so shellcheck warning about relative-path lookups can be ignored
# shellcheck disable=SC1091
. ./artifacts/sdk-build-env.sh
