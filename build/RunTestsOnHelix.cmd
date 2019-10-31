set MSBuildSDKsPath=%HELIX_CORRELATION_PAYLOAD%\s
set DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR=%MSBuildSDKsPath%
set MicrosoftNETBuildExtensionsTargets=%MSBuildSDKsPath%\Microsoft.NET.Build.Extensions\msbuildExtensions\Microsoft\Microsoft.NET.Build.Extensions\Microsoft.NET.Build.Extensions.targets
set DOTNET_ROOT=%HELIX_CORRELATION_PAYLOAD%\d
set PATH=%DOTNET_ROOT%;%PATH%
set TestFullMSBuild=%1
dir %HELIX_CORRELATION_PAYLOAD%\d
