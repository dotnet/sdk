# Dotnetup Version Metadata

Self-update uses readable full product version and RID metadata for local equality
checks. It has no independent build-identity digest or downloaded identity sidecar.
The former unshipped record and hidden option are not compatibility contracts.

## Scope and limitations

The [generator](../../../../src/Installer/VersionMetadata/VersionMetadataSource.cs)
embeds Arcade's full `$(Version)` and the artifact RID without hashing, truncating,
or stripping prerelease/build suffixes. These are the same inputs as
`ReturnDotnetupVersion` and the RID-specific artifact path. See
[Installer version settings](../../../../src/Installer/Directory.Build.props).

Two binaries with the same version and RID compare equal even if source, compiler
options, dependencies, configuration, or signing differ. Distinct released builds
must have distinct versions. Local rebuilds under the same `-dev` version cannot
be distinguished by this record. It is not a content hash, filesystem identity,
signature, freshness proof, or downgrade authorization.

The current channel and checksum metadata are unsigned. HTTPS pinning, SHA-512
checking, and embedded metadata comparisons detect consistency errors; they do
not authenticate a release or prevent replay/downgrade. Signed version manifests,
authenticated artifact hashes, and monotonic update authorization are explicitly
deferred to future stages. The existing signed release-manifest loader is not
wired into this self-update path.

## Record format

The record occupies 256 consecutive bytes, with no alignment or fixed file offset:

| Offset | Bytes | Value |
| --- | --- | --- |
| 0 | 16 | ASCII `DOTNETUP-VR-REC\0` |
| 16 | 4 | Unsigned little-endian format version: `1` |
| 20 | 4 | Reserved, zero |
| 24 | 224 | ASCII `full-version|rid`, followed by zero padding |
| 248 | 8 | ASCII `END-VER\0` |

Each field must be nonempty printable ASCII, excluding `|` and whitespace. The
combined payload, including its separator, must be at most 223 bytes so at least
one zero terminator fits. Oversized or ambiguous fields fail generation, never
silently truncate. The reader rejects missing, duplicate, truncated, malformed,
and unsupported records, including nonzero padding.

## Build and runtime access

The [shared targets](../../../../src/Installer/VersionMetadata/Dotnetup.VersionMetadata.targets)
forward the executable's version/RID through the command library to Installation,
which owns the generated record. Standalone library builds fall back to `TargetRid`.
Generation happens before `CoreCompile` and writes only changed bytes.
Referenced artifact outputs are isolated by configuration/RID.

The source uses a non-inlined UTF-8 `ReadOnlySpan<byte>` accessor. The live
[DotnetupProcessInfo](../../../../src/Installer/dotnetup.Library/DotnetupProcessInfo.cs)
startup reference roots the record in NativeAOT images on Windows and Unix.
Windows version resources alone would not cover Unix; native releases have no
managed DLL to reopen.

- [DotnetupVersionMetadata.Current](../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/VersionMetadata/DotnetupVersionMetadata.cs)
  reads the loaded image's immutable record, never its current executable pathname.
- [DotnetupVersionMetadataReader.Read(Stream)](../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/VersionMetadata/DotnetupVersionMetadataReader.cs)
  scans from the beginning through EOF with bounded memory, handles short/nonseekable
  reads and buffer boundaries, and leaves the stream open. Callers own secure opening.
- [NonSafeCommandGate](../../../../src/Installer/dotnetup.Library/SelfUpdate/NonSafeCommandGate.cs)
  compares loaded and canonical metadata while holding the activity lock.
  Cleanup and rollback likewise retain local version/RID consistency safeguards.

`ValidateDotnetupVersionMetadata` gates the RID-suffixed native publish copy,
including no-build publishing. [Final publishing](../../../../eng/Publishing.props)
revalidates the signed/stripped artifact offline. Neither step executes a
cross-compiled binary or emits an identity sidecar. See the
[host tooling guide](../../../../src/Installer/VersionMetadata/README.md).

## Startup smoke check

After replacement, [SelfUpdateVerifier](../../../../src/Installer/dotnetup.Library/SelfUpdate/SelfUpdateVerifier.cs)
runs the canonical executable with `--version`, telemetry disabled and the
first-run banner suppressed. A zero exit code and nonempty parseable version
output are required within 15 seconds. Output capture is bounded and the existing
cancellation, termination, and rollback paths remain in use.

This checks startup, not release authorization. The built-in version action
bypasses the command gate while the updater holds both locks. Exact local
version/RID consistency was already checked on the staged artifact.
