#! /usr/bin/env sh

# Install clang (required for NativeAOT/dotnet-aot builds)
# The base Debian image doesn't include LLVM packages, so add the LLVM apt repository
wget -qO- https://apt.llvm.org/llvm-snapshot.gpg.key | sudo tee /etc/apt/trusted.gpg.d/apt.llvm.org.asc > /dev/null
. /etc/os-release
echo "deb http://apt.llvm.org/${VERSION_CODENAME}/ llvm-toolchain-${VERSION_CODENAME}-18 main" | sudo tee /etc/apt/sources.list.d/llvm.list
sudo apt-get update && sudo apt-get install -y clang-18

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
