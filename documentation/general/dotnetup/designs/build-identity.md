# Dotnetup Build Identity

`dotnetup --build-identity` is the implemented, hidden root option used by
[self-update verification](self-update.md#stage-a-status). It reports the loaded
executable's identity without entering the command gate or starting telemetry.

## Contract

Every published dotnetup executable must contain exactly one immutable, versioned
identity record. Native publish validation enforces this contract. Running code
reads its own record from memory; the host tooling and offline runtime reader use
the same format without executing an artifact.

The build ID is 256 bits represented as 64 lowercase hexadecimal characters. It is an equality token, not a sortable version, a filesystem file ID, or an authentication mechanism.

This is distinct from the human-readable version exposed by [Parser.Version](../../../../src/Installer/dotnetup.Library/Parser.cs). The [publishing targets](../../../../eng/Publishing.props) pair each RID's binary and `.buildid` sidecar under the full version's artifact path. Generating and registering that metadata does not establish that a release has deployed it to the live daily feed.

## Automatic ID Policy

Use the existing Arcade `$(Version)` property, including its complete prerelease/build
suffix, and the artifact's RID. The shared version settings live in
[Directory.Build.props](../../../../src/Installer/Directory.Build.props); the same
version is returned by `ReturnDotnetupVersion` for the published artifact path.
For example, an official preview build can be `0.2.0-preview.1.26465.7`, while a
local development build can be `0.2.0-dev`.

To retain the fixed 64-character payload, [BuildIdentityMetadata](../../../../src/Installer/BuildIdentity/BuildIdentityMetadata.cs)
computes lowercase SHA-256 over the UTF-8
bytes of `dotnetup-version-id-v1\n{Version}\n{RID}`, with literal LF separators and
no terminal newline. This is just an encoding of version metadata, not a content
fingerprint. Missing or whitespace-containing metadata fails generation.

The same version and RID always produce the same ID. Source edits, compiler options,
dependencies, configuration, signing, renaming, and checkout paths do not independently
change it. There is no input collector, two-pass build, or post-link record stamping.
Distinct released builds for the same RID must have distinct versions; local rebuilds
under the same `-dev` version intentionally compare equal. Tests needing different
builds must supply different versions. The published hash/signature still validates
the executable bytes independently of this equality token.

No developer bumps a separate ID. The normal Arcade versioning process drives it,
and generation rewrites the record only when its contents change.

## Record Format

The record occupies 96 consecutive bytes, with no alignment requirement or fixed file offset:

| Offset | Bytes | Value |
| --- | --- | --- |
| 0 | 16 | ASCII `DOTNETUP-ID-REC\0` |
| 16 | 4 | Unsigned little-endian format version: `1` |
| 20 | 4 | Unsigned little-endian payload length: `64` |
| 24 | 64 | Lowercase hexadecimal build ID |
| 88 | 8 | ASCII `END-ID\0\0` |

The generator emits a C# UTF-8 byte literal exposed through a non-inlined
`ReadOnlySpan<byte>` accessor in the Installation assembly. The runtime accessor
reads the complete record. [DotnetupProcessInfo](../../../../src/Installer/dotnetup.Library/DotnetupProcessInfo.cs)
and [BuildIdentityAction](../../../../src/Installer/dotnetup.Library/BuildIdentityAction.cs)
provide live production references, so the record is rooted without the former
`IdentityRoots.xml` descriptor; that descriptor and its project item have been removed.

## Build Integration

The [identity targets](../../../../src/Installer/BuildIdentity/Dotnetup.BuildIdentity.targets)
are imported by the executable, command library, and Installation library. The
Installation library owns the generated record and the offline reader.

| Target | Owner and timing | Responsibility |
| --- | --- | --- |
| `ResolveDotnetupIdentityMetadata` | Before generation or reference forwarding | Read the final Arcade `Version` and the RID (`RuntimeIdentifier`, falling back to `TargetRid` for standalone library builds). |
| `ForwardDotnetupIdentityMetadata` | Executable and command library, before reference configuration | Pass the selected version and RID to the record-owning library. |
| `GenerateDotnetupBuildIdentity` | Installation library, before `CoreCompile` | Generate one record/accessor under `$(IntermediateOutputPath)` and include it in `Compile`. |
| `ValidateDotnetupBuildIdentity` | Executable, after native publishing | Read the executable offline; require exactly one valid record matching the version and RID. |

Generated source is always included and is written only when changed. Referenced
artifact builds isolate intermediate and assembly outputs by configuration and RID.
Standalone library/test builds use the shared Arcade version and target RID; they do
not need a separate identity policy.

The executable's `CreateRidSuffixedDotnetupCopy` depends on identity validation. A
`--no-build` publish validates the reused native artifact against the selected version
and RID; there is no saved manifest prerequisite. Generation and validation run as
host MSBuild tooling, never by executing a cross-compiled artifact.

[Publishing.props](../../../../eng/Publishing.props) validates the final distributable
again after signing/stripping, generates a sidecar containing the matching ID plus
LF, and registers it for `<concrete artifact URL>.buildid`. The existing `.sha512`
sidecar remains the integrity check. A release must deploy all three artifacts
before the live daily resolver can use them; this document does not assert that
the current public daily target already includes a sidecar.

The separate [host task project](../../../../src/Installer/BuildIdentity/Dotnetup.BuildIdentity.csproj)
is still required. It source-links the production reader without referencing the
record-owning Installation project, avoiding a generation dependency cycle. The
[task host](../../../../src/Installer/BuildIdentity/Dotnetup.BuildIdentity.proj) loads
an immutable, content-addressed shadow copy so reused Windows MSBuild nodes do not
lock compiler outputs. This host tooling can validate any target RID without
running the target executable. See the [tooling guide](../../../../src/Installer/BuildIdentity/README.md#why-separate-build-files-remain).

## Access and Update Gate

Implemented internal APIs:

- [DotnetupBuildIdentity.Current](../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/BuildIdentity/DotnetupBuildIdentity.cs)
	reads only the generated record in the loaded image. It does not reopen
	`Environment.ProcessPath` or read configuration.
- [DotnetupBuildIdentityReader.Read(Stream)](../../../../src/Installer/Microsoft.Dotnet.Installation/Internal/BuildIdentity/DotnetupBuildIdentityReader.cs)
	scans from the start through EOF with bounded memory, handles short reads and
	records spanning buffers, and rejects missing, duplicate, truncated, malformed,
	or unsupported records. It leaves the stream open; the caller owns secure file
	opening. Non-seekable streams are supported; seekable streams must start at zero.
- [NonSafeCommandGate](../../../../src/Installer/dotnetup.Library/SelfUpdate/NonSafeCommandGate.cs)
	compares the cached loaded ID with the securely opened canonical executable's ID
	while holding the activity lock. Missing or invalid identity never becomes an
	`unknown` equality fallback. Stage A rejects a mismatch before the command body.

New releases must retain the v1 record/read contract while older cooperating
clients require it. Because IDs encode version/RID rather than file contents,
two local rebuilds with identical metadata cannot be distinguished by this gate.

The [root option action](../../../../src/Installer/dotnetup.Library/BuildIdentityAction.cs)
reports the cached loaded ID plus a newline. [Program](../../../../src/Installer/dotnetup.Library/Program.cs)
bypasses telemetry startup, first-run notice, and flush for that action, so the
verification child can run while both update locks remain held. Offline equality
checks do not replace this execution check: a valid record alone does not prove
the executable can start. Before replacement, the workflow also requires the
hash-validated staged file's ID to match the pinned daily release ID. Identity is
not authentication; current daily self-update remains unsigned.
