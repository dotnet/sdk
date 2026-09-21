# Updating the SDK to a new target framework

This guide describes the annual rollover where the `main` branch moves from target
framework `net(N-1).0` to `netN.0`. Use a tracking issue for release-specific state,
owners, exact preview versions, and temporary workarounds. Keep this document focused on
the repeatable process and invariants.

Current example: [net12 transition tracking](https://github.com/dotnet/sdk/issues/55851).

## Recommended sequence

The work spans several repositories and should be coordinated in roughly this order.
Some steps can overlap, but the dependency and bootstrap steps must be ordered so the SDK
can build itself throughout the transition.

### 1. Prepare branches and branding

- Update the download/build table for the new `main` and the release candidate branches.
- Update branding so the release branches retain the intended `N-1` branding and `main`
  becomes `N.0.1xx-alpha`.
- Create the next `N-1` feature-band branch and configure the required inter-branch
  flows.
- Move localization after the appropriate release candidate snap.

Branding is best changed in the VMR so product repositories receive a consistent update.
Expect to fix or temporarily disable tests when the branding change flows back.

### 2. Add known framework references

Add the `netN.0` targeting contract in
[`GenerateBundledVersions.targets`](../../src/Layout/redist/targets/GenerateBundledVersions.targets)
before netN runtime packs are available. The live `netN.0` entries can initially resolve
to the current `N-1` product packs; they will move to netN packs when those dependencies
flow.

This change has two equally important parts:

1. Add complete live metadata for `netN.0`.
2. Freeze `net(N-1).0` so later netN package and RID changes cannot alter it.

#### Snapshot the previous TFM's package versions

Capture the exact flowed `N-1` runtime and targeting pack versions for:

- `Microsoft.NETCore.App`
- `Microsoft.WindowsDesktop.App`
- `Microsoft.AspNetCore.App`

Define version-specific properties for those values. While `N-1` is prerelease, use the
exact available package versions. After `N-1` releases, replace temporary pins with
`(N-1).0.$(VersionFeature...)` and remove the temporary follow-up comment.

While `N-1` is still the current TFM, keep its generated metadata aligned with the live
packs bundled in the SDK. Activate the frozen values only after the netN targeting packs
become current; otherwise the SDK advertises pack versions that are not installed and
isolated-source restores fail.

Do not leave the previous TFM bound to live `Microsoft*PackageVersion` properties. Those
properties will advance to netN when new dependencies flow.

#### Establish per-TFM RID boundaries

Create explicit `NetN*` item layers for every version-sensitive pack family, even when a
new layer initially inherits `Net(N-1)*` without adding RIDs:

- apphost packs
- CoreCLR runtime packs
- Mono runtime packs
- Crossgen2 packs, including portable RIDs
- ILCompiler packs, including portable RIDs
- NativeAOT runtime packs
- ASP.NET Core runtime packs

Point aggregate "current" aliases at the new `NetN*` layers. Point the previous TFM's
metadata directly at `Net(N-1)*` layers.

Add new RIDs only when the producing runtime/assets support them. New netN-only RIDs
belong in the `NetN*` layer so older TFMs do not gain unsupported packs. Shared,
non-versioned lists such as WindowsDesktop should remain shared unless the producing
repository introduces a TFM-specific difference.

#### Add the complete live TFM block

Copy the previous live TFM block and audit every copied major number, property, item
name, default version, and RID list. A complete block currently includes:

- `KnownFrameworkReference`
- `KnownAppHostPack`
- `KnownCrossgen2Pack`
- `KnownILCompilerPack`
- NativeAOT and Mono `KnownRuntimePack` entries
- `KnownILLinkPack`
- `KnownWebAssemblySdkPack`
- `KnownAspNetCorePack`
- WindowsDesktop, WPF, Windows Forms, and ASP.NET Core framework references

The new TFM uses live product package properties and aggregate current RID lists. The
previous TFM uses frozen package properties and version-specific RID lists.

Preserve intentional cross-TFM conventions only after checking the surrounding entries.
For example, older `KnownWebAssemblySdkPack` entries currently use the current runtime
package version rather than a per-TFM frozen property.

#### Validate the generated contract

Generate `Microsoft.NETCoreSdk.BundledVersions.props` through the normal layout target
and inspect the expanded output, not only the source target.

Verify that:

- one complete entry set exists for `netN.0`;
- netN uses current package properties and current RID aliases;
- `net(N-1).0` uses only frozen `N-1` versions and RID snapshots;
- changing a netN-only RID list does not change generated `net(N-1).0` metadata;
- generated `net(N-2).0` metadata is unchanged; and
- the dynamically generated bundled/current TFM still comes from the current targeting
  pack's `FrameworkList.xml`.

Run the focused framework-reference, default-runtime-version, and self-contained
runtime-pack tests that consume the built bundled versions props.

### 3. Flow product dependencies and update the bootstrap SDK

- Flow netN runtime, WindowsDesktop, and ASP.NET Core dependencies.
- Update `global.json` to a bootstrap SDK build that supports targeting netN.
- Update restore-toolset inputs for the `N-1` runtime where required.

Do not hand-edit generated dependency-flow properties. Follow the repository's
dependency-flow process so versions, manifests, and feeds remain consistent.

### 4. Retarget the SDK, tests, and templates

- Retarget the SDK to `netN.0`.
- Update the shared test framework constants in
  [`ToolsetInfo.cs`](../../test/Microsoft.NET.TestFramework/ToolsetInfo.cs).
- Retarget default templates to `netN.0`.
- Update build-time analyzer and package-generation commands that explicitly select the
  current TFM.

Use `PreviousTargetFramework` for tests that must temporarily remain on `N-1`. Mark
temporary transition changes with `NetTFMUpdate` so they can be found and removed later.

Templates from other repositories, such as WindowsDesktop templates, may need to remain
pinned to `N-1` until their netN packages are available.

### 5. Unwind temporary transition changes

- Re-enable or unpin tests and templates disabled during branding and runtime flow.
- Search for and remove all temporary `NetTFMUpdate` changes.
- Remove temporary `PreviousTargetFramework` usage that is no longer necessary.
- Replace exact `N-1` framework-pack pins after GA.
- Confirm the SDK, tests, templates, and documentation all describe netN as current.

## Common failure modes

- **Backward version leakage:** the previous TFM still references live current-product
  package properties and starts resolving netN packs after dependency flow.
- **Backward RID leakage:** the previous TFM references aggregate current RID lists and
  silently gains netN-only platforms.
- **Partial pack coverage:** only framework references are copied, omitting apphost,
  Crossgen2, NativeAOT, Mono, ILLink, WebAssembly, or internal ASP.NET Core metadata.
- **Copy/paste major mismatch:** comments, property names, or item names still identify
  the wrong TFM.
- **Premature hardcoding:** the current or maximum TFM is hardcoded instead of being read
  from the current targeting pack.
- **Untracked temporary changes:** disabled tests and pinned templates are not marked for
  cleanup.

## Historical examples

### net11

- [Transition tracker](https://github.com/dotnet/sdk/issues/50295)
- [Branding](https://github.com/dotnet/sdk/pull/50468)
- [Initial known framework references](https://github.com/dotnet/sdk/pull/50329)
- [CoreCLR Apple mobile RID separation](https://github.com/dotnet/sdk/pull/51429)
- [Additional next-TFM RID metadata](https://github.com/dotnet/sdk/pull/51855)
- [Update frozen net10 versions after release](https://github.com/dotnet/sdk/pull/51925)
- [Backflow from the VMR](https://github.com/dotnet/sdk/pull/52242)
- [Unwind temporary test changes](https://github.com/dotnet/sdk/pull/52512)

### net7

These older changes predate the current VMR and repository layout, but remain useful for
identifying less common transition surfaces:

- [SDK branding](https://github.com/dotnet/sdk/pull/20212)
- [Runtime flow and Blazor baselines](https://github.com/dotnet/sdk/pull/20859)
- [Feeds and runtime flow](https://github.com/dotnet/sdk/pull/19824)
- [Template package updates](https://github.com/dotnet/sdk/pull/27486)
- [Installer known framework references and bootstrap updates](https://github.com/dotnet/installer/pull/11750)
- [Post-release installer template update](https://github.com/dotnet/installer/pull/14961)
