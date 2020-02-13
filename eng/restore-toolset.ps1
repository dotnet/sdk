function InitializeCustomSDKToolset {
    if (-not $restore) {
      return
    }
  
    # Turn off MSBuild Node re-use
    $env:MSBUILDDISABLENODEREUSE=1
  
    # Workaround for the sockets issue when restoring with many nuget feeds.
    $env:DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=0
  
    # Enable vs test console logging
    $env:VSTEST_BUILD_TRACE=1
    $env:VSTEST_TRACE_BUILD=1
  
    $env:DOTNET_CLI_TELEMETRY_PROFILE='$env:DOTNET_CLI_TELEMETRY_PROFILE;https://github.com/dotnet/format'
  
    $cli = InitializeDotnetCli -install:$true
    InstallDotNetSharedFramework "2.1.15"
  }
  
  function InstallDotNetSharedFramework([string]$version) {
    $dotnetRoot = $env:DOTNET_INSTALL_DIR
    $fxDir = Join-Path $dotnetRoot "shared\Microsoft.NETCore.App\$version"
  
    if (!(Test-Path $fxDir)) {
      $installScript = GetDotNetInstallScript $dotnetRoot
      & $installScript -Version $version -InstallDir $dotnetRoot -Runtime "dotnet"
  
      if($lastExitCode -ne 0) {
        Write-Output "Failed to install Shared Framework $version. Ignoring failure as not all distros carrie all versions of the framework."
      }
    }
  }
  
  InitializeCustomSDKToolset