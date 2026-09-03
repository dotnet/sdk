# Redirecting analyzers in SDK to VS

We will redirect analyzers from the SDK to ones deployed in VS to avoid the [torn SDK][torn-sdk] issue at design time.
Only matching major+minor versions will be redirected because different versions of the same analyzer cannot be assumed to be compatible.
So this applies to a situation like:
- Having an analyzer in SDK 9.0.1 referencing Roslyn 4.12. That gets deployed to VS 17.12.
- Having an analyzer in SDK 9.0.7 referencing Roslyn 4.13.
- User loads project with SDK 9.0.7 in VS 17.12.
  Previously the analyzers would fail to load as they reference a newer Roslyn version (4.13) than what is part of VS (4.12).
  Now we will redirect the analyzers to those from SDK 9.0.1 that are deployed as part of VS and reference the matching Roslyn (4.12).

Loading older analyzer versions should not be a problem because they must reference an older version of Roslyn.

Targeting an SDK (and hence also loading analyzers) with newer major version in an old VS already results in an error like:

> error NETSDK1045: The current .NET SDK does not support targeting .NET 10.0.
> Either target .NET 9.0 or lower, or use a version of the .NET SDK that supports .NET 10.0.
> Download the .NET SDK from https://aka.ms/dotnet/download

## Overview

- The SDK will contain a VSIX with the analyzer DLLs and an MEF-exported implementation of `IAnalyzerAssemblyRedirector`.
  Implementations of this interface are imported by Roslyn and can redirect analyzer DLL loading.

- The SDK's implementation of `IAnalyzerAssemblyRedirector` will redirect any analyzer DLL matching some pattern
  to the corresponding DLL deployed via the VSIX.
  Details of this process are described below.

- Note that when `IAnalyzerAssemblyRedirector` is involved, Roslyn is free to not use shadow copy loading and instead load the DLLs directly.

- It is possible to opt out of analyzer redirecting by setting environment variable `DOTNET_ANALYZER_REDIRECTING=0`.
  That is an unsupported scenario though and compiler version mismatch errors will likely occur.

## Details

The VSIX contains some analyzers, for example:

```
AspNetCoreAnalyzers\analyzers\dotnet\cs\Microsoft.AspNetCore.App.Analyzers.dll
NetCoreAnalyzers\analyzers\dotnet\cs\System.Text.RegularExpressions.Generator.dll
WindowsDesktopAnalyzers\analyzers\dotnet\System.Windows.Forms.Analyzers.dll
SDKAnalyzers\Sdks\Microsoft.NET.Sdk\analyzers\Microsoft.CodeAnalysis.NetAnalyzers.dll
WebSDKAnalyzers\Sdks\Microsoft.NET.Sdk.Web\analyzers\cs\Microsoft.AspNetCore.Analyzers.dll
```

And metadata at `metadata.json`:

```json
{
  "AspNetCoreAnalyzers": "9.0.0-preview.5.24306.11",
  "NetCoreAnalyzers": "9.0.0-preview.5.24306.7",
  "WindowsDesktopAnalyzers": "9.0.0-preview.5.24306.8",
  "SDKAnalyzers": "9.0.100-dev",
  "WebSDKAnalyzers": "9.0.100-dev",
}
```

Given an analyzer assembly load going through our `IAnalyzerAssemblyRedirector`,
we will redirect it if the original path of the assembly being loaded matches the path of a VSIX-deployed analyzer -
only relevant segments (see example below) of these paths are compared,
plus the major and minor component of the versions must match.

For example, the analyzer

```
C:\Program Files\dotnet\sdk\9.0.100-preview.5.24307.3\Sdks\Microsoft.NET.Sdk\analyzers\Microsoft.CodeAnalysis.NetAnalyzers.dll
```

will be redirected to

```
{VSIX}\SDKAnalyzers\Sdks\Microsoft.NET.Sdk\analyzers\Microsoft.CodeAnalysis.NetAnalyzers.dll
```

where `metadata.json` has `"SDKAnalyzers": "9.0.100-dev"`, because
1. the suffix `Sdks\Microsoft.NET.Sdk\analyzers\Microsoft.CodeAnalysis.NetAnalyzers.dll` matches, and
2. the version `9.0.100-preview.5.24307.3` has the same major and minor component (`9.0`) as the version `9.0.100-dev`
   (both versions are read from the paths, not DLL metadata).

Analyzers that cannot be matched will continue to be loaded from the SDK
(and will fail to load if they reference Roslyn that is newer than is in VS).

### Implementation

Analyzer DLLs are contained in transport package `VS.Redist.Common.Net.Core.SDK.RuntimeAnalyzers`.
The redirecting logic lives in "system" VS extension `Microsoft.Net.Sdk.AnalyzerRedirecting`.

[torn-sdk]: https://github.com/dotnet/sdk/issues/42087
