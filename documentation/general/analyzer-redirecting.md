# Redirecting analyzers in SDK to VS

We will redirect analyzers from the SDK to ones deployed in the VS to avoid the [torn SDK][torn-sdk] issue.
Only major versions will be redirected because different major versions of the same analyzer cannot be assumed to be compatible.
So this applies to a situation like:
- Having an analyzer in SDK 9.0.1 referencing Roslyn 4.12. That gets deployed to VS 17.12.
- Having an analyzer in SDK 9.0.7 referencing Roslyn 4.13.
- User loads project with SDK 9.0.7 in VS 17.12.
  Previously the analyzers would fail to load as they reference newer Roslyn version (4.13) than what is part of VS (4.12).
  Now we will redirect the analyzers to those from SDK 9.0.1 that are deployed as part of VS and reference the matching Roslyn (4.12).

Loading analyzers with older major version should not be a problem because they must reference an older version of Roslyn.

Targeting an SDK (and hence also loading analyzers) with newer major version in an old VS already results in an error like:

> error NETSDK1045: The current .NET SDK does not support targeting .NET 10.0.  Either target .NET 9.0 or lower, or use a version of the .NET SDK that supports .NET 10.0. Download the .NET SDK from
https://aka.ms/dotnet/download

## Overview

- The SDK will contain [a mapping file][file-format] listing the analyzers that want to [dual-insert][dual-insert] into VS.

- Those analyzers will be deployed with VS in some `{VS-analyzers}` folder
  (to be determined, could be something like `C:\Program Files\Microsoft Visual Studio\2022\Preview\Common7\IDE\DotNetAnalyzers`).

  - This will happen as part of the SDK being inserted into VS.
  
  - The mapping file [will be used][vs-insertions] to automatically gather all the analyzers.

  - The mapping file will be deployed there as well (at `{VS-analyzers}\mapping.txt`) so Roslyn can read it.

    - If multiple SDKs are inserted into VS, the most recent version wins.
      The mapping file should be backwards-compatible (we should not remove patterns from it).

- Roslyn [will use][roslyn-redirecting] the mapping file to redirect analyzer loads from SDK to VS.

  - If the file is not found at `{VS-analyzers}\mapping.txt`, analyzer loading continues normally from SDK.

  - If a DLL load is requested which matches a mapping, it is redirected via the [IAnalyzerAssemblyResolver][analyzer-assembly-resolver] infrastructure.

## Mapping file format
[file-format]: #mapping-file-format

The mapping file v1 is a simple list of directory "patterns". For example:

```
packs/Microsoft.NETCore.App.Ref/*/analyzers/dotnet/cs
sdk/*/Sdks/Microsoft.NET.Sdk/analyzers
sdk/*/Sdks/Microsoft.NET.Sdk.Web/analyzers/cs
```

Each pattern can contain a single asterisk `*` where a version is expected.

Lines starting with `#` are ignored (can be used as comments).

### Inserting analyzers into VS
[vs-insertions]: #inserting-analyzers-into-vs

The file is used to copy analyzers into VS.
The paths start at the dotnet root.
Only the major version from the `*` part is preserved.
Rest of the path is preserved in the destination.
So for example given the following pattern

```
packs/Microsoft.NETCore.App.Ref/*/analyzers
```

we copy

```
{dotnet-root}\packs\Microsoft.NETCore.App.Ref\8.0.7\analyzers\**
```

into

```
{VS-analyzers}\packs\Microsoft.NETCore.App.Ref\8\analyzers\**
```

If multiple SDKs are inserted into VS,
they should have different major versions,
but if there are two SDKs trying to copy the same pattern with the same major version,
an error will be reported (can be changed later to have only the more recent SDK win if needed).

### Redirecting analyzer loads in Roslyn
[roslyn-redirecting]: #redirecting-analyzer-loads-in-roslyn

The file is used to redirect DLL loads in Roslyn.
We do not rely on knowledge of `{dotnet-root}`, we simply try to match any path containing the pattern.
For example the following pattern

```
packs/Microsoft.NETCore.App.Ref/*/analyzers
```

matches

```
C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.7\analyzers\dotnet\cs\Microsoft.Interop.ComInterfaceGenerator.dll
```

and redirects load of that DLL into load of

```
{VS-analyzers}\packs\Microsoft.NETCore.App.Ref\8\analyzers\dotnet\cs\Microsoft.Interop.ComInterfaceGenerator.dll
```

Analyzers that cannot be matched will continue to be loaded from the SDK
(and will fail to load if they reference Roslyn that is newer than the VS).

### Future extensibility

If needed, the format can be extended/changed in the future together with the Roslyn redirecting implementation.
Since Roslyn and SDK (which contains the file) are inserted into VS approximately together,
there should be no problems with the file and the implementation that reads it being incompatible.
We could add a version into the file name (like `mapping.v2.txt`) so it is simply ignored
in the short transition period when a different SDK is inserted into VS than Roslyn
(relevant only to internal preview VS builds).

## Alternatives

- The file could live in the Roslyn repo, deployed together with Roslyn into VS.
  That would make updating the format and the implementation easier if needed.
  The SDK would have to extract it from a Roslyn transport package to use it for inserting the analyzer DLLs.

[torn-sdk]: https://github.com/dotnet/sdk/issues/42087
[dual-insert]: https://github.com/dotnet/sdk/blob/8a2a7d01c3d3f060d5812424a9de8a00d70b3061/documentation/general/torn-sdk.md#net-sdk-in-box-analyzers-dual-insert
[analyzer-assembly-resolver]: https://github.com/dotnet/roslyn/blob/dabd07189684b5cda34b3072326a12b18301a012/src/Compilers/Core/Portable/DiagnosticAnalyzer/IAnalyzerAssemblyResolver.cs#L12
