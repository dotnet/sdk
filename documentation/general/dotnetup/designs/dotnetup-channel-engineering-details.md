#### Dotnetup Version Engineering Details

While [dotnetup-channel-policy.md](../dotnetup-channel-policy.md) outlines what each `channel` of `dotnetup` means for customers to help them decide which `channel` of `dotnetup` to subscribe to, this document outlines how each version will be used by our engineering team, and abstractly what our intent is per version. The actual release process from version to version is defined elsewhere, see https://github.com/dotnet/sdk/pull/54735.

### `Daily` Channel Versions

`daily` versions are meant for our own engineering and testing.

The `main` branch of the .NET SDK will be a first party consumer of `dotnetup` `daily` builds.

Each `dotnetup` branch will use a `preview` implementation of `dotnetup` to acquire the .NET SDK needed to build itself and then that `branch` of `dotnetup` itself to acquire any tooling (runtimes/sdks) to build the .NET SDK on that branch containing `dotnetup`. This prevents a `daily` dotnetup bug from blocking the build of a fix to a `daily` `dotnetup` version.

We may implement backward compatibility for breaking changes even in `daily` builds at our discretion but in general should avoid doing so.

### `Preview` Channel Versions

We will closely monitor telemetry for potential bugs or regressions added between the `preview` and `daily` build before promoting a `daily` to a `preview` build.
`preview` versions may be built off the top of `preview` or `lts/sts` versions of .NET.

It will be easier to document breaking changes for the `stable` version if we document them at `preview` time, so we should aim to write breaking change notices. We don't do this for `daily` versions considering they may change rapidly/multiple times before their changes make it to a `stable` version.

.NET teams may use `preview` versions in their own build infrastructure. This allows us to catch problems early by dogfooding our own product.

### `Stable` Channel Versions

Every `stable` release begins as a `daily` build that is promoted to `preview` and then to `stable`. We should provide both notice and backward compatibility for breaking changes when within reason.

`stable` builds will be built off of `stable` .NET Runtimes, and thus be in a separate, stable branch. This means we have a large time window (Feb-Aug during the GA and until the RC build of a new .NET Major) where the `stable` version of `dotnetup` may lag behind `preview` versions and is best maintained with a separate branch, aka the `release/stable/dotnetup` branch. Codeflow between the `daily` and `preview` branch to the `stable` branch will not be automated.

The first runtime supporting a `stable` build will be `.NET 11`.

### Patches

A patch update to the embedded runtime causes a patch update to `dotnetup`.
