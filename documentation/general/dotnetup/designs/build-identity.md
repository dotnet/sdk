# Dotnetup Build Identity

`dotnetup --build-identity` must be added to support `self update` for a replacement mechanism to cheaply detect `dotnetup` artifact versions.

## Contract

Every dotnetup executable should contain one immutable, versioned identity record. Running code reads its own record from memory; an offline reader reads the same format from an executable file without executing it.

The build ID is 256 bits represented as 64 lowercase hexadecimal characters. It is an equality token, not a sortable version, a filesystem file ID, or an authentication mechanism.

This is distinct from the existing human-readable version exposed by [Parser.Version](../../../../src/Installer/dotnetup.Library/Parser.cs) and the [versioned artifact paths](../../../../eng/Publishing.props). Release metadata records both the version and the build ID for each RID.

## Automatic ID Policy

Compute `SHA256` over a canonical, versioned build-input manifest, prefixed with the domain separator `dotnetup-build-id-v1`.

The proposed input collector must describe the complete dotnetup artifact build, not just the project containing the generated record:

- Content hashes of effective source files, generated sources other than this record, and resources across the executable and its project dependencies. Local and untracked files included by MSBuild count, so an uncommitted edit changes the ID.

- Content identities of resolved managed dependencies, native libraries, generators, analyzers, and compiler/linker toolchains. Restore must complete before collecting resolved inputs; package names or version strings alone do not identify locally modified dependencies.

- Effective build settings affecting compilation or native publishing, including TFM, RID, configuration, defines, optimization, trimming, globalization, version attributes, and native compiler/linker options.

Use structured serialization with a fixed schema, ordered fields and entries, invariant encoding, and stable logical paths. Exclude timestamps, machine-specific checkout/output paths, this generated record, and outputs derived from it. The record version and fingerprint-policy version are separate: changing input collection changes the policy prefix, not necessarily the binary layout.

Identical effective inputs produce the same ID, including after a clean rebuild; changed inputs produce a different ID, subject to SHA-256 collision resistance. Signing or renaming the same artifact does not allocate a new ID. A missing required input must fail identity generation rather than produce an incomplete fingerprint. Complete input collection is an implementation requirement, not a property established by the prototype.

No developer bumps the ID. Normal build/publish computes the manifest, updates the record only when its contents change, and carries the resulting ID through artifact validation and release metadata. CI uses the same policy as local builds. A reproducibility test must check that equivalent builds in different checkout paths produce the same ID.

## Record Format

The record occupies 96 consecutive bytes, with no alignment requirement or fixed file offset:

| Offset | Bytes | Value |
| --- | --- | --- |
| 0 | 16 | ASCII `DOTNETUP-ID-REC\0` |
| 16 | 4 | Unsigned little-endian format version: `1` |
| 20 | 4 | Unsigned little-endian payload length: `64` |
| 24 | 64 | Lowercase hexadecimal build ID |
| 88 | 8 | ASCII `END-ID\0\0` |

Generate a C# UTF-8 byte literal exposed through a `ReadOnlySpan<byte>` accessor in one owning assembly. The runtime accessor must reference the complete record so NativeAOT retains it as program data.

## Build Integration

Use one proposed dotnetup-specific targets import, explicitly imported by [dotnetup.Library](../../../../src/Installer/dotnetup.Library/dotnetup.Library.csproj) and [dotnetup](../../../../src/Installer/dotnetup/dotnetup.csproj), with targets conditioned on their owning project.

| Target (proposed) | Owner and timing | Responsibility |
| --- | --- | --- |
| `CollectDotnetupIdentityInputs` | Artifact build orchestration, after restore and other input generation, before the library compiles | Collect the evaluated artifact graph and effective build inputs into the canonical manifest. Pass the same artifact context to referenced projects. |
| `GenerateDotnetupBuildIdentity` | Library, before `CoreCompile`, dependent on input collection | Hash the manifest and generate one record/accessor under `$(IntermediateOutputPath)`. Include that source in `Compile`. |
| `ValidateDotnetupBuildIdentity` | Executable, after native publishing | Read the published executable offline; require exactly one valid record matching the expected manifest ID. |

The generated source must be included even when generation is incrementally skipped. Collect content changes on every relevant build, but write the manifest/source only when changed; avoid a timestamp-only cache that misses restored or same-timestamp edits. Intermediate files and project build contexts must distinguish artifact configurations/RIDs so concurrent builds cannot overwrite each other's identities. Standalone library/test builds need an automatically collected library context; an executable publish must not reuse that context as its artifact identity.

The existing executable already enables `PublishAot` and invokes `Publish` from `PublishOnBuild`. Make `ValidateDotnetupBuildIdentity` an explicit dependency of its existing `CreateRidSuffixedDotnetupCopy` target so validation occurs before the distributable copy. Only validate when this build produces or reuses a native artifact. A `--no-build` publish must validate the reused artifact against its matching saved input manifest, or fail if that manifest is unavailable.

Implement collection/generation and offline validation as host-runnable build tooling, not by invoking the cross-compiled executable. Keep fingerprint logic out of long MSBuild property expressions. Reuse the production record parser in validation. Validate the final distributable again after stripping/signing, then extract its ID automatically into per-RID release metadata; reject an inconsistent record.

## Access and Update Gate

Proposed runtime APIs:

```csharp
string loadedId = DotnetupBuildIdentity.Current;
using FileStream executable = File.OpenRead(installedPath);
string installedId = DotnetupBuildIdentityReader.Read(executable);
bool sameBuild = string.Equals(loadedId, installedId, StringComparison.Ordinal);
```

`Current` reads only the generated record in the loaded image. It does not reopen `Environment.ProcessPath` or read configuration. `Read` scans one opened regular file using bounded memory and handles records spanning buffer boundaries. It scans to EOF to detect duplicates and rejects missing, duplicate, truncated, malformed, or unsupported records. Never accept an `unknown` fallback. New releases must retain the v1 record/read contract while older cooperating clients require it.

`--build-identity` reports `Current`, with no telemetry or gate, for the updater's existing post-replacement execution check. Offline equality checks do not replace that check: a valid record alone does not prove the executable can start. Before replacement, the updater also requires the validated staged file's ID to match the channel's per-RID build ID. Both the record and its claimed release association remain subject to the separate hash/signature policy.
