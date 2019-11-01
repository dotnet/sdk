#!/bin/bash

export MSBuildSDKsPath=$HELIX_CORRELATION_PAYLOAD/s
export DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR=$MSBuildSDKsPath
export MicrosoftNETBuildExtensionsTargets=$MSBuildSDKsPath/Microsoft.NET.Build.Extensions/msbuildExtensions/Microsoft/Microsoft.NET.Build.Extensions/Microsoft.NET.Build.Extensions.targets
export DOTNET_ROOT=$HELIX_CORRELATION_PAYLOAD/d
export PATH=$DOTNET_ROOT:$PATH
