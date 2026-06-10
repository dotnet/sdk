# Microsoft.NET.Sdk.Testing — prototype

**Status:** prototype for the design proposed in
[dotnet/sdk#49294](https://github.com/dotnet/sdk/issues/49294). Not yet wired
into templates, not yet validated end-to-end, MTP-only path is the only well-
exercised one.

## What this is

A new MSBuild SDK that lives in-band in the .NET SDK and lets test projects
declare what framework + platform they want without manually wiring up
`PackageReference` entries and runner properties. Versions of the test
frameworks themselves come from NuGet at user-pinned versions — nothing
framework-specific is shipped in-band.

## What a consuming project looks like

xUnit v3 on MTP (most common case) — uses the SDK's pinned default version, no version property required:

```xml
<Project Sdk="Microsoft.NET.Sdk.Testing">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <TestFramework>XUnit</TestFramework>
  </PropertyGroup>
</Project>
```

Same thing, pinning to a specific xUnit version (recommended for libraries that want hermetic builds):

```xml
<Project Sdk="Microsoft.NET.Sdk.Testing">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <TestFramework>XUnit</TestFramework>
    <XUnitVersion>3.0.0</XUnitVersion>
  </PropertyGroup>
</Project>
```

Opt into floating to always pick up the latest stable xUnit on each restore (non-deterministic; use only with a NuGet lock file if you care about reproducibility):

```xml
<Project Sdk="Microsoft.NET.Sdk.Testing">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <TestFramework>XUnit</TestFramework>
    <XUnitVersion>*</XUnitVersion>
  </PropertyGroup>
</Project>
```

NUnit on VSTest (explicit platform opt-out of the MTP default):

```xml
<Project Sdk="Microsoft.NET.Sdk.Testing">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <TestFramework>NUnit</TestFramework>
    <TestPlatform>VSTest</TestPlatform>
  </PropertyGroup>
</Project>
```

xUnit test-helpers library (no Exe, lower-level packages):

```xml
<Project Sdk="Microsoft.NET.Sdk.Testing">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <TestFramework>XUnit</TestFramework>
    <IsTestUtilityProject>true</IsTestUtilityProject>
  </PropertyGroup>
</Project>
```

## Property surface (prototype scope)

Required:
- `TestFramework` — `MSTest` | `NUnit` | `XUnit` | `TUnit` | `Expecto`. `XUnit` always means xUnit v3.

Optional:
- `<Framework>Version` (e.g. `XUnitVersion`, `MSTestVersion`, `NUnitVersion`, `TUnitVersion`, `ExpectoVersion`) — defaults to a value pinned by this SDK (see `Targets/_DefaultPackageVersions.props`). Override with a fixed version to lock the test framework, or with `*` (or `*-*`) to float to the latest stable (or latest including prerelease). Floating restores are not reproducible without a NuGet lock file.
- `TestPlatform` — `MicrosoftTestingPlatform` (alias `MTP`) | `VSTest`. Defaults from framework.
- `IsTestUtilityProject` — defaults `false`. When `true`: no `Exe` output, no `Microsoft.NET.Test.Sdk` reference, framework metapackage swapped for the assert/extensibility packages.
- `TestingExtensionsProfile` — MTP-only. `Default` | `AllMicrosoft` | `None`. Mirrors `MSTest.Sdk`.
- `EnableMicrosoftTestingExtensions<Name>` and `MicrosoftTestingExtensions<Name>Version` — per-extension toggles and version overrides; mirror `MSTest.Sdk`.
- Companion-package versions (`XUnitRunnerVisualStudioVersion`, `NUnit3TestAdapterVersion`, `NUnitAnalyzersVersion`, `YoloDevExpectoTestSdkVersion`, `MicrosoftNETTestSdkVersion`, `MicrosoftTestingPlatformVersion`) — all have pinned defaults; override the same way as the framework version.

Framework × platform validity:

| Framework | MTP | VSTest | Default when omitted |
|-----------|-----|--------|----------------------|
| MSTest    | ✅   | ✅      | MTP                  |
| NUnit     | ✅   | ✅      | MTP                  |
| XUnit (v3)| ✅   | ✅      | MTP                  |
| TUnit     | ✅   | ❌      | MTP                  |
| Expecto   | ✅   | ✅      | MTP                  |

Invalid pairs error at evaluation.

## Layout

Mirrors `src/WebSdk/Worker/` so MSBuild's SDK resolver picks the SDK up the same way:

```
src/TestingSdk/
├── Sdk/
│   ├── Sdk.props            # MSBuild SDK entry point
│   └── Sdk.targets
├── Targets/
│   ├── Microsoft.NET.Sdk.Testing.props      # Dispatch + validation
│   ├── Microsoft.NET.Sdk.Testing.targets
│   ├── _DefaultPackageVersions.props        # Centralized pinned defaults (user-overridable)
│   ├── Frameworks/                          # One file pair per framework
│   │   ├── {MSTest,NUnit,XUnit,TUnit,Expecto}.{props,targets}
│   └── Platforms/
│       ├── MicrosoftTestingPlatform.{props,targets}
│       └── VSTest.{props,targets}
└── Tasks/
    └── Microsoft.NET.Sdk.Testing.Tasks.csproj   # Packaging only; no compiled code
```

The packaging csproj sets `PackageLayoutOutputPath` to
`artifacts/bin/<Configuration>/Sdks/Microsoft.NET.Sdk.Testing/` (via the
shared `src/WebSdk/CopyPackageLayout.targets`), and
`src/Layout/redist/targets/GenerateLayout.targets` copies that into the
redist SDK layout next to `Sdks/Microsoft.NET.Sdk.Worker/` etc.

## Known gaps in the prototype

- **MSTest path is naive.** A real implementation should delegate to
  `MSTest.Sdk` rather than re-implementing it.
- **No template integration.** `dotnet new mstest/nunit/xunit` still emit the
  classic `Microsoft.NET.Sdk` + `Microsoft.NET.Test.Sdk` PackageReference shape.
  Template changes live in `dotnet/templating`.
- **Aspire / Playwright / NativeAOT-MSTest.Engine** features that
  `MSTest.Sdk` exposes via `Features/` are not yet ported.
- **`<Framework>PackageSource`** (Arcade-style local-path override) is not
  implemented; v2 work.
- **Not yet exercised by a test asset** in `test/TestAssets/TestProjects`.
- **Localized strings** (the `<Error Text="...">` content) live inline; a
  real version would move them to a `.resx` and `.xlf` set.
