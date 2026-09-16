# Dotnetup Build Identity Tooling

The [binary contract](../../../documentation/general/dotnetup/designs/build-identity.md)
identifies dotnetup releases using existing Arcade version metadata. It does not
attempt to identify all effective compiler inputs or prove byte-for-byte equivalence.

[BuildIdentityMetadata](BuildIdentityMetadata.cs) encodes the full `$(Version)` and
RID as lowercase SHA-256 of `dotnetup-version-id-v1\n{Version}\n{RID}`. The fixed
record remains 96 bytes. The hash is only a fixed-size representation of metadata;
the executable's published hash/signature provides integrity/authentication separately.

Rebuilding the same version and RID produces the same identity even after source
edits or compiler changes. Distinct releases must use distinct versions. Local
builds using `0.2.0-dev` intentionally compare equal for the same RID.

## Runtime Contract

- [DotnetupBuildIdentity.Current](../Microsoft.Dotnet.Installation/Internal/BuildIdentity/DotnetupBuildIdentity.cs)
  returns the loaded record's 64 lowercase hexadecimal characters. It never opens
  the running executable by path.
- [DotnetupBuildIdentityReader.Read(Stream)](../Microsoft.Dotnet.Installation/Internal/BuildIdentity/DotnetupBuildIdentityReader.cs)
  reads from the beginning through EOF without closing the stream. It supports
  non-seekable streams and short reads with bounded memory. A seekable stream must
  initially be at offset zero. Invalid data throws `InvalidDataException`; I/O
  exceptions propagate. The caller owns opening and securing the file.
- The APIs are internal to `Microsoft.Dotnet.Installation.Internal`, available to
  the existing friend assemblies. Host tooling source-links the production reader.

## Pipeline

1. [ResolveDotnetupIdentityMetadata](Dotnetup.BuildIdentity.targets) reads Arcade's
  final `Version` during target execution, not during the earlier project import.
  The version and RID are forwarded through the two dotnetup project references;
  standalone library builds fall back to `TargetRid` when `RuntimeIdentifier` is empty.
2. The host task generates the record before Installation's `CoreCompile`, always
  includes it in `Compile`, and writes it only when changed. Artifact-reference
  outputs are isolated by configuration and RID. No source enumeration, toolchain
  fingerprint, input manifest, or two-pass build is involved.
3. [DotnetupProcessInfo](../dotnetup.Library/DotnetupProcessInfo.cs) captures
  `Current` during [native startup](../dotnetup.Library/Program.cs), and
  [BuildIdentityAction](../dotnetup.Library/BuildIdentityAction.cs) reads that
  cached value. These live production references root the record; its accessor
  remains non-inlined to preserve the complete record as program data. The former
  `IdentityRoots.xml` descriptor and its project item have been removed.
4. Native validation gates `CreateRidSuffixedDotnetupCopy`, including no-build
  publishing. Validation reads the artifact offline and requires exactly one record
  matching the selected version and RID. It never executes the target binary.
5. [Publishing.props](../../../eng/Publishing.props) revalidates the final artifact
  against `ReturnDotnetupVersion` and `TargetRid` after signing/stripping, then emits
  and registers `<concrete binary URL>.buildid` containing the ID plus LF. Metadata
  and binary share the versioned blob path. `.sha512` remains the integrity-check
  surface. Live daily availability requires a release deploying those artifacts;
  local generation does not demonstrate that the public feed already has them.

Task assemblies are loaded from content-addressed shadow copies to avoid locking
compiler outputs in reused Windows MSBuild nodes. Copies live under each project's
intermediate directory so concurrent project contexts do not overwrite a loaded
task assembly. Unchanged copies are reused.

## Why Separate Build Files Remain

- [Dotnetup.BuildIdentity.csproj](Dotnetup.BuildIdentity.csproj) builds the host
  MSBuild task independently of the product's RID. It links the offline reader,
  not the Installation project, so generation cannot depend on its own output and
  validation never executes a cross-compiled artifact.
- [Dotnetup.BuildIdentity.proj](Dotnetup.BuildIdentity.proj) evaluates `UsingTask`
  with the completed shadow-copy path supplied as a global property. Loading the
  task directly from its mutable compiler output would reintroduce Windows locks.
- [The targets](Dotnetup.BuildIdentity.targets) forward version/RID metadata from
  the executable through the command library to Installation, generate its record,
  and validate publishing. No direct executable-to-Installation reference is needed.
  [The props](Dotnetup.BuildIdentity.props) keep artifact-reference outputs separate
  from standalone managed library/test outputs.
- [Final publishing](../../../eng/Publishing.props) reuses
  `BuildDotnetupIdentityTool`, including when `NoBuild=true`; only the host helper
  is rebuilt. The already signed/stripped product artifact is read, not rewritten.
  There is no prebuilt-helper glob or saved input manifest prerequisite.

## Validation

[Identity tests](../../../test/dotnetup.Tests/DotnetupBuildIdentityTests.cs) cover
version/RID equality, metadata changes, missing metadata, malformed and duplicate
records, short reads, and write-only-on-change generation. The test build verifies
that the loaded ID matches the single record in the owning managed assembly.
Final NativeAOT publishing and post-signing validation still require the platform's
native toolchain; managed tests alone do not prove that final artifact path. The
[self-update review guide](../../../documentation/general/dotnetup/designs/self-update.md#environment-and-validation-boundaries)
records the bounded Windows/Linux x64 native validation status and the unverified
macOS boundary. The [native fixture](../../../test/dotnetup.Tests/Utilities/NativeSelfUpdateFiles.cs)
requires two NativeAOT executables with different full versions, supplied through
`DOTNETUP_TEST_EXECUTABLE` and `DOTNETUP_TEST_REPLACEMENT`; changing source alone
does not change their identity.