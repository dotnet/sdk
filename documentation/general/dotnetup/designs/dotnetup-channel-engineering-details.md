#### Dotnetup Version Engineering Details

While [dotnetup-version-policy.md] outlines what each `channel` of `dotnetup` means for customers in terms of breaking change notices, this document outlines how each version will be used by our engineering team, and abstractly how we will promote from one version to another. The actual release process from version to version is defined elsewhere.

### `Daily` Channel Versions

`daily` versions are meant for our own engineering and testing.

The `main` branch of the .NET SDK will be a first party consumer of `dotnetup` `daily` builds.

Each `dotnetup` branch will use a `preview` implementation of `dotnetup` to acquire the .NET SDK needed to build itself and then that `branch` of `dotnetup` itself to acquire any tooling (runtimes/sdks) to build the .NET SDK on that branch containing `dotnetup`. This prevents a `daily` dotnetup bug from blocking the build of a fix to a `daily` `dotnetup` version.

We may implement backward compatibility for breaking changes even in `daily` builds at our discretion but in general should avoid doing so.

### `Preview` Channel Versions

We will closely monitor telemetry for potential bugs or regressions added between the `preview` and `daily` build before promoting a `daily` to a `preview` build.
`preview` versions may be built off the top of `preview` or `lts/sts` versions of .NET.

.NET teams may use `preview` versions in their own build infrastructure. This allows us to catch problems early by dogfooding our own product.

### `Stable` Channel Versions

Every `stable` release begins as a `daily` build that is promoted to `preview` and then to `stable`.
We should provide both notice and backward compatibility for breaking changes when within reason.

`stable` builds will be built off of `stable` .NET Runtimes, and thus be in a separate, stable branch.
The first runtime supporting a `stable` build will be `.NET 11`.
