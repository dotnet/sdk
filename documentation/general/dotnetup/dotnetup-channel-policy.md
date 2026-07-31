# `dotnetup` Release Channel Definitions

## Overview

`dotnetup` refers exclusively to the standalone developer toolchain that manages .NET.

This document describes the intended audience and quality bar for each `dotnetup` release channel to help you choose a channel. It is not an official support policy. The [.NET Support Policy](https://dotnet.microsoft.com/platform/support/policy) page will contain the official `dotnetup` policy when one is available.

## Channels

The term `channel` in this document refers to one of three `dotnetup` release qualities. Each channel has a different audience and quality bar.

`channel` may also refer to the .NET SDK or .NET Runtime version subscribed to by `dotnetup`, which is better outlined in [the getting started documentation](README.md#step-1-choose-a-channel).

`dotnetup` is a standalone tool with a lifecycle that is independent of the .NET SDK lifecycle.
`dotnetup` releases in three channels:

- `daily`
- `preview`
- `stable`

Unlike the .NET SDK or .NET Runtime, `dotnetup` is a tip-only product, meaning only the latest version per supported `channel` is maintained.

Supported fixes and updates are only provided through the latest `stable` channel version. When a new `stable` version is published, it immediately supersedes the previous `stable` version. New `daily` and `preview` versions may continue to be published, but those channels have a lower quality bar and are not recommended for use in production. This intent may change as the product matures or when an official support policy supersedes this document.

### `Daily` Channel Versions

`daily` builds of `dotnetup` will be fresh out of CI, much like the builds of the .NET SDK that are sometimes called "nightly" builds.

We do not recommend using `daily` builds in production. They have no guarantees, no fix timelines, and no support. Features may be added, changed, or removed without notice. Breaking changes may be added with or without documentation or notice.

`daily` is a slight misnomer, as multiple `daily` builds may be published in one day.

### `Preview` Channel Versions

`preview` versions are not officially supported but they are offered for public testing ahead of a promotion to a `stable` release. At our discretion, `daily` builds will be promoted to `preview` versions.

Breaking change notices may be published in the [.NET Docs](https://github.com/dotnet/docs) if we expect high impact. However, `preview` versions may contain breaking changes without notice. No service-level agreement or fix timeline applies to a `preview` version.

### `Stable` Channel Versions

Only the most recently published `stable` version of `dotnetup` is supported. Supported in this case means that the latest `stable` version may receive security and bug fixes.

Intentional breaking changes will be documented as breaking change notices in the [.NET Docs](https://github.com/dotnet/docs).

When a new `stable` version of `dotnetup` is published, previous versions no longer receive fixes, updates, or online technical assistance.

At this time, historical `stable` versions of `dotnetup` will remain available for download as official [dotnet releases](https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json) despite their lack of official support, much like historical .NET versions are available today.

PSAs and CVEs are planned to be announced through the [.NET release notes](https://github.com/dotnet/core), following a similar convention to other .NET releases. Discussions and release tags will exist in the [.NET SDK repository](https://github.com/dotnet/sdk).

## Semantic Versioning

`preview` and `stable` versions of `dotnetup` follow [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html). For example:

- `1.2.4-preview.1` identifies a `preview` version.
- `1.2.4` identifies a `stable` version.

`daily` versions do not follow Semantic Versioning. They are intended for rapid engineering iteration and may introduce breaking changes without a corresponding major version increment.
