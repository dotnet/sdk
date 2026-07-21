# `dotnetup` Support Policy

## Overview

`dotnetup` refers exclusively to the standalone, self-contained tool for developers that helps them manage and acquire .NET.

`dotnetup` is an experimental CLI and still changing and evolving. We are monitoring adoption and feedback which will inform support policy decisions and whether to treat `dotnetup` as a first-class product in the long-term.

Looking for the support policy for another part of the .NET platform? See the [.NET Support Policy](https://dotnet.microsoft.com/platform/support/policy) page.

Every Microsoft product has a lifecycle. The lifecycle begins when a product is released and ends when it's no longer supported. Knowing key dates in this lifecycle helps you make informed decisions about when to upgrade or make other changes to your software. This product is governed by [Microsoft's Modern Lifecycle Policy](https://learn.microsoft.com/lifecycle/policies/modern).

Unlike the .NET SDK, `dotnetup` is a tip-only product. Only the most recently published `stable` version of `dotnetup` is supported. When a new `stable` version is published, it immediately supersedes the previous `stable` version. `daily` and `preview` versions are not supported. This policy may change as the product matures. `dotnetup` is a standalone tool with a lifecycle that is independent of the .NET SDK lifecycle.

`dotnetup` releases in three channels:

- `daily`
- `preview`
- `stable`

Each channel's support policy is outlined below.

### Supported Platforms and Shells

The latest `stable` version of `dotnetup` is supported on Windows, macOS, and Linux versions supported by its embedded .NET Runtime. See the [.NET supported operating system policy](https://github.com/dotnet/core/blob/main/os-lifecycle-policy.md) for the applicable operating system lifecycle rules.

Shell integration, including profile modification and environment-script generation, is supported for Bash (`bash`), Z shell (`zsh`), fish (`fish`), and Windows PowerShell (`powershell`) alongside PowerShell Core (`pwsh`). Other shells are not supported for shell integration.

### `Daily` Channel Versions

`daily` builds of `dotnetup` will be fresh out of `CI`, much like the `daily` or `nightly` builds of the .NET SDK.
We do not recommend using `daily` builds in production. They have no guarantees, no support, and are meant for our own engineering and testing. They are used at your own discretion.

The `daily` `dotnetup` builds will use themselves to build. This allows us to catch problems early by being a dogfooder of our own product.

Multiple `daily` builds may be published in one day. Only the most recent `daily` build is intended for dog food engineering use; older `daily` builds are not maintained.

#### Support Policy

The `daily` version of `dotnetup` does not have any guarantees. Features may be added, changed, or removed without notice. Breaking changes may be added without documentation or notice. We may provide notices or backward compatibility for breaking changes even in `daily` builds; however, this is not an official policy and is at our discretion depending upon the feature and expected risk.

The `main` branch of the .NET SDK will also be a first party consumer of `dotnetup` `daily` builds.

If the `daily` build of `dotnetup` is broken, we will not guarantee any SLA or timeline to fix it.

### `Preview` Channel Versions

At our discretion, `daily` builds may be promoted to `preview` versions. We will closely monitor telemetry for potential bugs or regressions added between the `preview` and `daily` build before promoting the `daily` to a `preview` build. `preview` versions are not officially supported by Microsoft and should not be used in production or other high-priority environments.

`preview` versions may be built off the top of `preview` or `lts/sts` versions of .NET.

`preview` versions may contain breaking changes without notice. No service-level agreement or fix timeline applies to a `preview` version. Regardless, we aim to publish breaking change notices for expected high-impact changes.

.NET teams may use `preview` versions in its own build infrastructure. This internal use does not make `preview` versions supported for customer production use.

### `Stable` Channel Versions

Only the most recently published `stable` version of `dotnetup` is supported. Supported `stable` versions receive security and bug fixes as patch versions. `daily` versions will eventually catch up with features added in `preview` versions.

`dotnetup` does not yet have a public timeline for the first`stable` release.

`dotnetup` versioning is inspired by [Semantic Versioning](https://semver.org/) but does not strictly implement it. Major, minor, and patch version components communicate the expected scope of a release, but they are not compatibility guarantees; more detail is provided below. Intentional breaking changes will be documented regardless of which version component changes. In general, we will avoid breaking changes and introduce them only when we believe they provide substantial benefits.

`stable` builds will be built off of `stable` .NET Runtimes.
The first runtime supporting a `stable` build will be `.NET 11`.

`stable` versions of `dotnetup` follow the [.NET Runtime servicing policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core#servicing). When a .NET Runtime servicing release updates the runtime embedded in `dotnetup`, the corresponding `dotnetup` release may be published up to three days later. The current `stable` version of `dotnetup` remains supported during this publication window.

#### Major `stable` Release

Major releases may include new features and functionality, new public APIs, bug fixes, and intentional [breaking changes](https://learn.microsoft.com/dotnet/core/compatibility/breaking-changes).

#### Minor `stable` Release

Minor releases may include new features, new functionality, new public APIs, and bug fixes. Their scope is generally smaller than that of major releases.

#### Patches & Servicing

Patch versions may include security fixes, bug fixes, and updates to the self-contained .NET Runtime embedded in the executable. A patch version is not intended to introduce new features. A patch update to the embedded runtime causes a patch update to `dotnetup`.

#### End of support

End of support refers to the date when Microsoft no longer provides fixes, updates, or online technical assistance for a product version.

When a new `stable` version of `dotnetup` is published, all previous versions immediately reach end of support.

`dotnetup` is a free product. The Modern Lifecycle Policy's 12-month notification commitment before ending support without a successor does not apply to free products or preview releases. Despite this exception, we will provide at least six months' notice before retiring the `stable` channel without a successor. This retirement notice does not apply when one `stable` version supersedes another under the tip-only support model.

Historical `stable` versions of `dotnetup` will generally remain available for download as official [dotnet releases](https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json) despite their lack of official support, much like historical .NET versions are available today.

PSAs and CVEs are planned to be announced under the [same platform (dotnet release notes)](https://github.com/dotnet/core) used for dotnet releases today, following a similar convention. However, discussions and release tags will exist on the [.NET SDK repository](https://github.com/dotnet/sdk).

### FAQ

**How can I update to the latest `dotnetup` version?**

Once `stable` releases are available, follow the [download instructions](README.md#download-dotnetup) and select the `stable` build quality. Pass `--quality stable` to `get-dotnetup.sh` or `-Quality stable` to `get-dotnetup.ps1`. Running either script without this option currently installs a `daily` build, which are available today but not supported.
