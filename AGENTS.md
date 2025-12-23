# Agent Instructions for Working on the .NET SDK

This document provides comprehensive instructions for AI agents working on the dotnet/sdk repository, particularly for Static Web Assets related changes.

## Repository Structure

The SDK repository contains multiple components. Key paths for Static Web Assets:

```
src/StaticWebAssetsSdk/
├── Tasks/                          # MSBuild task implementations
│   ├── Data/                       # Data structures (StaticWebAsset, StaticWebAssetEndpoint, etc.)
│   ├── Utils/                      # Utilities (PathTokenizer, OSPath, etc.)
│   └── *.cs                        # Task implementations
├── Targets/                        # MSBuild targets (.targets files)
└── Sdk/                            # SDK props and targets

test/Microsoft.NET.Sdk.StaticWebAssets.Tests/
├── StaticWebAssets/                # Unit tests for tasks
└── *.cs                            # Integration tests
```

## Development Workflow

### 1. Building the SDK Tasks

To build the Static Web Assets tasks:

```powershell
cd src/StaticWebAssetsSdk/Tasks
dotnet build
```

The output DLL will be at:
```
artifacts/bin/Sdks/Microsoft.NET.Sdk.StaticWebAssets/tasks/net10.0/Microsoft.NET.Sdk.StaticWebAssets.Tasks.dll
```

### 2. Running Unit Tests

Run specific test classes:

```powershell
dotnet test test/Microsoft.NET.Sdk.StaticWebAssets.Tests/Microsoft.NET.Sdk.StaticWebAssets.Tests.csproj --filter "FullyQualifiedName~YourTestClassName"
```

Never run all the tests, as that takes a very long time. Leave that to the CI.

### 3. Testing with a Local SDK (E2E Validation)

This is the most important workflow for validating fixes against real projects.

#### Step 1: Build Your Changes

```powershell
cd src/StaticWebAssetsSdk/Tasks
dotnet build
```

#### Step 2: Use Your Local SDK

First, try using your system's installed .NET SDK:

```powershell
# Find SDK location and version
$sdkVersion = dotnet --version
$sdkRoot = Split-Path (Get-Command dotnet).Source
Write-Host "SDK version: $sdkVersion at $sdkRoot"
```

#### Step 3: Patch the Local SDK

Copy the built DLL to your local SDK:

```powershell
$sdkVersion = dotnet --version
$sdkRoot = Split-Path (Get-Command dotnet).Source
$sdkTasksPath = "$sdkRoot\sdk\$sdkVersion\Sdks\Microsoft.NET.Sdk.StaticWebAssets\tasks\net10.0"

Copy-Item -Path "artifacts\bin\Sdks\Microsoft.NET.Sdk.StaticWebAssets\tasks\net10.0\Microsoft.NET.Sdk.StaticWebAssets.Tasks.dll" `
          -Destination $sdkTasksPath -Force
```

#### Step 4: Test with the Patched SDK

Build/publish your test project:

```powershell
dotnet build MyProject.csproj
dotnet publish MyProject.csproj -c Release -o publish
```

#### Alternative: Download a Fresh SDK (if needed)

If you encounter NuGet package resolution issues or version conflicts with your local SDK, download a fresh isolated SDK:

```powershell
$sdkVersion = "10.0.101"  # Or desired version
$downloadUrl = "https://aka.ms/dotnet/10.0.1xx/daily/dotnet-sdk-win-x64.zip"
$localSdkPath = "D:\work\samples\MyProject\dotnet"

# Download and extract
Invoke-WebRequest -Uri $downloadUrl -OutFile "dotnet-sdk.zip"
Expand-Archive -Path "dotnet-sdk.zip" -DestinationPath $localSdkPath -Force

# Patch the downloaded SDK
$sdkTasksPath = "$localSdkPath\sdk\$sdkVersion\Sdks\Microsoft.NET.Sdk.StaticWebAssets\tasks\net10.0"
Copy-Item -Path "artifacts\bin\Sdks\Microsoft.NET.Sdk.StaticWebAssets\tasks\net10.0\Microsoft.NET.Sdk.StaticWebAssets.Tasks.dll" `
          -Destination $sdkTasksPath -Force

# Use the isolated SDK
& "$localSdkPath\dotnet.exe" build MyProject.csproj
