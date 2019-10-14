REM cannot modify HELIX_CORRELATION_PAYLOAD so copy to workitem payload
robocopy %HELIX_CORRELATION_PAYLOAD%\t t /E
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command t\eng\dogfood.ps1
set MSBuildSDKsPath=%HELIX_CORRELATION_PAYLOAD%\s
set DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR=%MSBuildSDKsPath%
set MicrosoftNETBuildExtensionsTargets=%MSBuildSDKsPath%\Microsoft.NET.Build.Extensions\msbuildExtensions\Microsoft\Microsoft.NET.Build.Extensions\Microsoft.NET.Build.Extensions.targets
set DOTNET_ROOT=t\.dotnet
set PATH=%DOTNET_ROOT%;%PATH%
