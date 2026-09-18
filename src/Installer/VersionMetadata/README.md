# Dotnetup Version Metadata Tooling

The [record contract](../../../documentation/general/dotnetup/designs/version-metadata.md)
preserves the full Arcade `$(Version)` and RID as readable metadata. There is no
digest of those fields, independent version token, or published metadata sidecar.
Same-version/RID rebuilds compare equal regardless of source or compiler changes.
This is local consistency metadata, not authentication or freshness authorization.

## Pipeline

1. [The targets](Dotnetup.VersionMetadata.targets) resolve final Arcade `Version`
   during execution and select `RuntimeIdentifier`, falling back to `TargetRid`.
   The executable and command library forward these values to Installation.
2. [VersionMetadataSource](VersionMetadataSource.cs) generates the 256-byte record
   before Installation's `CoreCompile`, including it on every build and writing
   only changed content. [The props](Dotnetup.VersionMetadata.props) isolate
   artifact-reference outputs by configuration and RID.
3. [DotnetupProcessInfo](../dotnetup.Library/DotnetupProcessInfo.cs) reads the
   generated non-inlined accessor during startup, rooting it in NativeAOT. The
   offline reader shares the format but never loads or executes the artifact.
4. Validation gates `CreateRidSuffixedDotnetupCopy`, including no-build publishing,
   and requires exactly one record matching the selected full version and RID.
5. [Publishing.props](../../../eng/Publishing.props) revalidates the final
   signed/stripped binary. The executable and existing SHA-512 checksum are
   published; there is no additional sidecar.

## Why host build files remain

[Dotnetup.VersionMetadata.csproj](Dotnetup.VersionMetadata.csproj) builds a
host-only MSBuild task that source-links the production offline reader, avoiding
a dependency cycle through the Installation library it helps compile.
[The task host](Dotnetup.VersionMetadata.proj) loads the task from an immutable
shadow copy so reused Windows MSBuild nodes do not lock compiler outputs.
The copy's content hash only isolates the build tool's assembly; it is not
embedded in or published with dotnetup.

Generation and validation do not execute a target binary, so cross-RID native
publishing retains its validation. There is no input manifest, input collector,
two-pass compilation, or post-link stamping.

## Validation

[Record tests](../../../test/dotnetup.Tests/DotnetupVersionMetadataTests.cs) cover
full version/RID preservation, size limits, malformed/duplicate/truncated records,
short reads, buffer boundaries, and write-only-on-change generation.
[Native fixtures](../../../test/dotnetup.Tests/Utilities/NativeSelfUpdateFiles.cs)
require two NativeAOT executables with different full versions via
`DOTNETUP_TEST_EXECUTABLE` and `DOTNETUP_TEST_REPLACEMENT`.
[Live self-update tests](../../../test/dotnetup.Tests/SelfUpdateEndToEndTests.cs)
exercise replacement and no-op polling on private copies on Windows and Linux.
They require a reachable daily release with the command, embedded version metadata,
and checksum, and smoke-test startup via `--version`.

Signed version manifests and monotonic update authorization remain future work.
The current unsigned checksum and readable record must not be treated as proof
of authenticated freshness or downgrade protection.
