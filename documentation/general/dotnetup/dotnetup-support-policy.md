# `dotnetup` Support Policy

## Overview

`dotnetup` refers exclusively to the standalone, self-contained tool for developers that helps them manage and acquire .NET.

`dotnetup` is an experimental CLI and still changing and evolving. We are monitoring adoption and feedback which will inform support policy decisions and whether to treat `dotnetup` as a first-class product in the long-term.

Looking for the support policy for another part of the .NET platform? See the [.NET Support Policy](https://dotnet.microsoft.com/platform/support/policy) page.

Every Microsoft product has a lifecycle. The lifecycle begins when a product is released and ends when it's no longer supported. Knowing key dates in this lifecycle helps you make informed decisions about when to upgrade or make other changes to your software. This product is governed by [Microsoft's Modern Lifecycle Policy.](https://learn.microsoft.com/lifecycle/policies/modern)

 Unlike the .NET SDK, will release as a tip only product, meaning that *only the latest version* of `dotnetup` will remain in support at any given time. This may change as the product matures, and if it becomes more engrained into the .NET ecosystem. To be clear `dotnetup` is a standalone tool isolated outside of the .NET SDK space.

#### `dotnetup` will release in three separate version `channels`:
`daily`
`preview`
`stable`

Each channel support policy will be outlined below.

### `Daily` Channel Versions

`daily` builds of `dotnetup` will be fresh out of `CI`, much like the `daily` or `nightly` builds of the .NET SDK.
We do not recommend using `daily` builds in production. They have no guarantees, no support, and are meant for our own engineering and testing. They are used at your own discretion.

The `daily` `dotnetup` builds will use themselves to build. This allows us to catch problems early by being a dogfooder of our own product.

`daily` builds may actually contain multiple builds per day. Using a `dotnetup` build besides the current build would be a problem, since a buggy `daily` build would break the ability to build a new `daily` with a fix.

#### Support Policy

The `daily` version of `dotnetup` does not have any guarantees. Features may be added, changed, or removed without notice. Breaking changes may be added without documentation or notice. For extreme cases, or changes that may incur a large amount of pain for `daily` build consumers, we may decide to bake in legacy support for `daily` scenarios, but we will err on the side of removing that support to avoid creating technical debt.

The `main` branch of the .NET SDK will also be a first party consumer of `dotnetup` `daily` builds.

If the `daily` build of `dotnetup` is broken, we will not guarantee any SLA or timeline to fix it.

### `Preview` Channel Versions

At our discretion, `daily` builds will be promoted to `preview` versions, following the semantic version policy.
For production software, we recommend developers only use `preview` versions of `dotnetup` for `preview` development environments. For high-priority environments, we recommend against using `preview` versions.

A `daily` version should generally be promoted only after it has shown at minimum `3` days of solid data proving it did not introduce any fatal new bugs or regressions.

`preview` versions will be built off the top of `preview` versions of .NET.

`preview` versions may also contain breaking changes. High-impact behavioral changes should receive a breaking change notice. Behavioral changes that customers could have expected may or may not receive a breaking change notice or document.

Production versions of the .NET SDK and other .NET products may rely on the `preview` version of `dotnetup` in `ci` or local build infrastructure. We do this to be a first-party dogfooder of our own `preview` versions, and because for critical scenarios, we can hold ourselves accountable and quickly contact the right local experts when something goes wrong.

### `Stable` Channel Versions

Only the latest `stable` version of `dotnetup` will be supported. The `stable` version of `dotnetup` will receive security and quality updates as patch versions.

`dotnetup` does not yet have a public timeline for the first `stable` release.

Breaking changes must follow `semantic versioning` and must be documented. Breaking changes will be documented as such in our standard [dotnet documentation](https://github.com/dotnet/docs). In general, we will avoid a breaking change and only do so if we believe it provides significant positive impact or customer benefit.

`stable` builds will be built off of `stable` .NET Runtimes.
The first runtime supporting a `stable` build will be `.NET 11`.

The `stable` version of `dotnetup` must release monthly alongside updates to the `latest`, in-support `.NET Runtime`, adopting their servicing schedule. However, `stable` versions of `dotnetup` will generally be promoted off well-tested `preview` builds. A `stable` version should not be released off a `preview` version before conclusive data and software validation have validated the new `stable` branded build. A patch on top of an existing `stable` version that does not promote changes from the `preview` version will likewise be validated before release, and may act as a `preview` version to gather data before its release, excluding of course security or other related patches.

#### Major `stable` Release

Major releases include new features and functionality, new public APIs, and bug fixes. Due to the nature of the changes, these releases are expected to include [breaking changes](https://learn.microsoft.com/dotnet/core/compatibility/breaking-changes?WT.mc_id=dotnet-35129-website).

#### Minor `stable` Release

Minor release includes new features and functionality; however, the difference between major and minor releases is generally smaller than between major releases.

#### Patches & Servicing

Patch versions will exist only to fix critical bugs or to provide security updates. Runtime `patch` version bumps for the self-contained runtime embedded within the executable will also cause a bump in the `patch` of `dotnetup` `stable`.

#### End of support

End of support refers to the date when Microsoft no longer provides fixes, updates, or on-line technical assistance. Furthermore, you can't update or send in new applications to the Microsoft Store with .NET Native toolchains that are no longer supported.

Any version of `dotnetup` `stable` that is not latest will immediately reach `End of Support`. This allows for a fast moving and evolving product to deliver higher impact to customers.

Any deprecation or end of life policy for the `stable` channel `dotnetup` would be receive at minimum a `6` month notice in the event that support for `dotnetup` ended. As `dotnetup` matures and potentially replaces other core products, such as the [.NET Install Scripts](https://learn.microsoft.com/dotnet/core/tools/dotnet-install-script), we intend to increase that notice policy.

Historical `stable` versions of `dotnetup` will generally remain available for download as official [dotnet releases](https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json) despite their lack of official support, much like historical .NET versions are available today.

PSAs and CVEs are planned to be announced under the [same platform (dotnet release notes)](https://github.com/dotnet/core) used for dotnet releases today, following a similar convention. However, discussions and release tags will exist on the [.NET SDK repository](https://github.com/dotnet/sdk).

### FAQ

**How can I update to the latest `dotnetup` version?**

Please see our documentation (pending) once `dotnetup` reaches `stable` version support.
