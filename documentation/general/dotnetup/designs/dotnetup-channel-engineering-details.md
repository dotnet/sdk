# `dotnetup` Release Channel Engineering Details

While [dotnetup-channel-policy.md](../dotnetup-channel-policy.md) describes the customer-facing expectations for each `dotnetup` channel, this document describes how the engineering team intends to produce and use those channels. The release process is proposed separately in [dotnet/sdk#54735](https://github.com/dotnet/sdk/pull/54735).

## `Daily` Channel Versions

`daily` versions are meant for our own engineering and testing.

The `main` branch of the .NET SDK will be a first-party consumer of `dotnetup` `daily` builds.

Each `dotnetup` branch will use a `preview` version of `dotnetup` to acquire the .NET SDK needed to build itself. It will then use the version of `dotnetup` from that branch to acquire any other runtimes or SDKs required to build the .NET SDK. This prevents a bug in a `daily` version from blocking the build of its own fix.

We may implement backward compatibility for breaking changes in `daily` builds at our discretion, but in general should avoid doing so.

## `Preview` Channel Versions

We will closely monitor telemetry for potential bugs or regressions in a `daily` build before selecting it to produce a `preview` build.
`preview` versions may be built with preview or supported .NET versions.

The preview release pipeline must assign the intended numeric version components and the prerelease label at build time. Updating only `PreReleaseVersionLabel` is insufficient: the pipeline must also update the minor and patch components as appropriate for the preview version being produced.

We should aim to write breaking change notices when a change reaches `preview`, making the notices available before the change reaches `stable`. We do not require notices for `daily` versions because they may change rapidly or multiple times before promotion.

.NET teams may use `preview` versions in their own build infrastructure. This allows us to catch problems early by dogfooding our own product.

## `Stable` Channel Versions

Every `stable` release begins with artifacts from a selected `daily` build that are used to produce a separately versioned `preview` build before the release reaches `stable`. We should provide both notice and backward compatibility for breaking changes when reasonable.

The .NET Runtime used to build the native `dotnetup` application is an implementation detail, but it affects how the product is built and serviced. `stable` builds will initially use a stable .NET Runtime. The branch for the `stable` channel will be named `release/stable/dotnetup`. The `daily` and `preview` channels may move to newer .NET versions while `stable` remains on that branch. Code flow from the development branch to `release/stable/dotnetup` will not be automated.

The first runtime supporting a `stable` build will be `.NET 11`.

## Servicing

When the embedded runtime includes a security or bug fix, we will produce a new `stable` version of `dotnetup`. This is essentially every .NET runtime [Release](https://dotnet.microsoft.com/platform/support/policy/dotnet-core#servicing), so it defines the release cadence for `dotnetup` `stable`. Features should generally be released or merged into `stable` outside of the timeframe where a security patch is incoming (.NET Release Tuesdays) to prevent any complications with regressions blocking customers on top of a required security patch. Release timing should be coordinated with .NET servicing rather than governed by a fixed delay.
