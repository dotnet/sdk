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
