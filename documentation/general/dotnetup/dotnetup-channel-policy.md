# `dotnetup` `daily`, `preview`, and `stable` Channel Definitions

## Overview

`dotnetup` refers exclusively to the standalone, self-contained tool for developers that helps them manage and acquire .NET.
`dotnetup` is a fast-moving CLI that is still changing and evolving.

Looking for the support policy for the .NET Platform? See the [.NET Support Policy](https://dotnet.microsoft.com/platform/support/policy) page.
`dotnetup`'s official support policy document is still underway; here, we aim to communicate version intent to allow users to pick which version of `dotnetup` to use.

Unlike the .NET SDK, `dotnetup` is a tip-only product. It is versioned into three separate `channels` that outline the quality-bar for each new version release for that channel.

Fixes and updates are only provided for the latest `stable` channel's version.  When a new `stable` version is published, it immediately supersedes the previous `stable` version. `daily` and `preview` channel versions may receive updates but have a lower bar and are not recommended for use in production. This policy may change as the product matures or when an official support policy page supersedes this document.

`dotnetup` is a standalone tool with a lifecycle that is independent of the .NET SDK lifecycle.
`dotnetup` releases in three channels:

- `daily`
- `preview`
- `stable`

### Supported Platforms and Shells

The latest `stable` version of `dotnetup` is supported on Windows, macOS, and Linux versions supported by its embedded .NET Runtime. See the [.NET supported operating system policy](https://github.com/dotnet/core/blob/main/os-lifecycle-policy.md) for the applicable operating system support policies of the .NET Runtime.

### `Daily` Channel Versions

`daily` builds of `dotnetup` will be fresh out of `CI`, much like the `daily` or sometimes tokened `nightly` builds of the .NET SDK.
We do not recommend using `daily` builds in production. They have no guarantees, no fix timelines, and no support.

Multiple `daily` builds may be published in one day.
Features may be added, changed, or removed without notice. Breaking changes may be added with or without documentation or notice.


### `Preview` Channel Versions

At our discretion, `daily` builds may be promoted to `preview` versions. `preview` versions are not officially supported but they are offered for public testing ahead of a promotion to a `stable` release.

Breaking change notices may be published in the [.NET Docs](https://github.com/dotnet/docs) if we expect they may have high impact. However, `preview` versions may contain breaking changes without notice. No service-level agreement or fix timeline applies to a `preview` version.

### `Stable` Channel Versions

Only the most recently published `stable` version of `dotnetup` is supported. Supported in this case means that `stable` versions receive security and bug fixes as patch versions. Every `stable` release begins as a `daily` build that is promoted to `preview` and then to `stable`; there is no separate feature stream that needs to "catch up."

`dotnetup` versioning is inspired by [Semantic Versioning](https://semver.org/) but does not strictly implement it. Intentional breaking changes will be documented as breaking change notices in the [.NET Docs](https://github.com/dotnet/docs).

`stable` builds will be built off of `stable` .NET Runtimes.
The first runtime supporting a `stable` build will be `.NET 11`.

`stable` versions of `dotnetup` follow the [.NET Runtime servicing policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core#servicing). When a .NET Runtime servicing release updates the runtime embedded in `dotnetup`, the corresponding `dotnetup` release may be published up to three days later.

# Semantic Versioning

`dotnetup` follows [semver v2.0.0](https://semver.org/spec/v2.0.0.html).

#### Patches & Servicing

Patch versions may include security fixes, bug fixes, and updates to the self-contained .NET Runtime embedded in the executable. A patch version is not intended to introduce new features. A patch update to the embedded runtime causes a patch update to `dotnetup`.

#### End of support

End of support refers to the date when Microsoft no longer provides fixes, updates, or online technical assistance for a product version.

When a new `stable` version of `dotnetup` is published, all previous versions immediately reach end of support.

At this time, historical `stable` versions of `dotnetup` will remain available for download as official [dotnet releases](https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json) despite their lack of official support, much like historical .NET versions are available today.

PSAs and CVEs are planned to be announced under the [same platform (dotnet release notes)](https://github.com/dotnet/core) used for dotnet releases today, following a similar convention. Discussions and release tags will exist on the [.NET SDK repository](https://github.com/dotnet/sdk).
