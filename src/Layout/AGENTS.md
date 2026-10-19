# Layout Agent Instructions

Guidance for changes under `src/Layout`.

`src/Layout` **assembles and packages the shippable SDK**. It consumes
already-built components (the CLI, templates, SDKs, workload manifests, runtimes) and
lays them out into the redist directory and OS installers. It rarely implements
product behavior — most changes here are about *what gets bundled* and *how it's
packaged*.

## Where things live

| Path | Role |
|------|------|
| `redist/` | Composes the SDK layout: `redist.csproj` + the `targets/` that copy components into the redist. Also hosts the `dnx` launcher scripts. |
| `pkg/{deb,osx,windows}` | Native OS installer authoring (Debian/RPM, macOS `.pkg`, Windows MSI/bundle inputs). |
| `finalizer/` | Native Windows executable run during MSI/bundle install/uninstall that maintains the SDK installation registry records. |
| `VS.Redist.Common.*` | Visual Studio redist authoring projects — package SDK components for the VS installer. |

### Inside `redist/targets`

Two families of targets:

- **`Bundled*.targets` — *what* ships inside the SDK.** Each
  declares the components to bundle as MSBuild items.
- **`Generate*.targets` — *how* it's laid out and packaged.**

## Conventions & invariants

- **Bundled-component versions flow in from `eng/Version.Details.{xml,props}`**
  (managed by dependency flow / darc). To bundle or bump a component, set its version
  there and reference the generated `$(<Name>PackageVersion)` property from the
  matching `Bundled*.targets` — **never hardcode a version** in a Layout target. (For
  example, a bundled template is a `<BundledTemplate Include="..."
  PackageVersion="$(...)"/>` item whose version is defined in `Version.Details`.)
- Producing the laid-out SDK requires the **full repo build** (so the components exist
  to copy), not just this project — see the root build/dogfood instructions.

### Workload manifest layout ownership

[`LayoutManifests`](redist/targets/BundledManifests.targets) may only delete the
workload manifest files it owns; leave all other files in the layout alone. See
[`WorkloadManifestLayout.cs`](../Tasks/sdk-tasks/WorkloadManifestLayout.cs) for
which paths count as owned.
Downloaded package inputs come from NuGet's normalized version directory, but layout
destinations retain the supplied version spelling; the package-path resolver in
[`WorkloadManifestLayout.cs`](../Tasks/sdk-tasks/WorkloadManifestLayout.cs) keeps
these separate.

Test stale-file cleanup by running `LayoutManifests` directly. A full installer
build first deletes the whole destination in
[`LayoutBundledComponents`](redist/targets/GenerateInstallerLayout.targets), which
hides cleanup bugs.

### Workload metadata and root shim ownership

In [`GenerateInstallerLayout.targets`](redist/targets/GenerateInstallerLayout.targets),
`LayoutWorkloadUserLocalMarker` owns only `metadata/workloads/<feature-band>/userlocal`
in the redist staging tree. It removes obsolete markers, including when source-only
mode is disabled, but preserves other workload metadata and files under unrecognized
band directories. Empty recognized band directories can be pruned; shared parent
directories and nonempty bands are not removed.

`LayoutDnxShim` owns only the root `dnx` and `dnx.cmd` staging files.
`LayoutIntermediateDnxShim` owns the same pair in intermediate installer staging,
where neither file is included on Windows/macOS because the sharedhost installer owns
the launcher. Platform selection follows the build host, not the target RID.
[`WorkloadMetadataLayout.cs`](../Tasks/sdk-tasks/WorkloadMetadataLayout.cs) and
[`GetDnxShimLayout.cs`](../Tasks/sdk-tasks/GetDnxShimLayout.cs) constrain
discovery to owned paths.
State files are not deletion authority.
