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
Installation declares it as a build-only project reference, so ordinary restore
prepares the host tool along with the product. Compilation and publish validation
build the tool without invoking restore, including when the product uses
`--no-restore` or no-build publishing. Product RID, AOT, publish-directory, and
version-record properties are removed when building the host tool; its assembly
is not a product runtime dependency. The tool also clears product RID/AOT globals
in its own project because NuGet's restore graph walk can forward them despite
the project reference's `GlobalPropertiesToRemove` metadata.
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
[Build-graph tests](../../../test/dotnetup.Tests/DotnetupMetadataBuildTests.cs)
restore Installation into an isolated directory, verify that the host tool's assets
were restored without a product RID, and reject any nested `Restore` during a
subsequent no-restore build or no-build validation-tool invocation. They cover
ordinary and cross-RID/AOT product properties and check that the task assembly
does not enter the product output.
[Native fixtures](../../../test/dotnetup.Tests/Utilities/NativeSelfUpdateFiles.cs)
require two NativeAOT executables with different full versions via
`DOTNETUP_TEST_EXECUTABLE` and `DOTNETUP_TEST_REPLACEMENT`, built for the same RID.
The [dotnetup test jobs](../../../eng/pipelines/templates/jobs/dotnetup/dotnetup-tests.yml)
publish two fixture versions into separate artifact directories on Windows and Linux,
and set `DOTNETUP_TEST_REQUIRE_NATIVE=true` so missing configuration fails rather
than silently skipping native scenarios. The ordinary publish remains separate.
Local runs with neither executable variable set remain opt-in; configuring only
one path is an error. macOS native self-update scenarios remain OS-skipped.
[Live self-update tests](../../../test/dotnetup.Tests/SelfUpdateEndToEndTests.cs)
exercise replacement and no-op polling on private copies on Windows and Linux.
They require a reachable daily release with the command, embedded version metadata,
and checksum, and smoke-test startup via `--version`.

Signed version manifests and monotonic update authorization remain future work.
The current unsigned checksum and readable record must not be treated as proof
of authenticated freshness or downgrade protection.
