# `Microsoft.NET.Build.Containers`

This folder contains the source code for .NET app containers (`Microsoft.NET.Build.Containers`).

The documentation on .NET app containers is available in [dotnet/sdk-containers-builds](https://github.com/dotnet/sdk-container-builds/tree/main/docs) repo.

In case you encounter an issue with .NET app containers or would like to submit a feature request, please use [dotnet/sdk-containers-builds](https://github.com/dotnet/sdk-container-builds/issues/new) repo.

If you would like to contribute by creating a pull request, please refer to [dotnet/sdk contributing guide](https://github.com/dotnet/sdk#how-do-i-engage-and-contribute). Ideally, prior starting the effort find a corresponding issue in [dotnet/sdk-containers-builds](https://github.com/dotnet/sdk-container-builds/issues) repo and let us and others know in a comment. If you plan to address the problem that is not reflected in any issue, please create one. Consider helping us triaging the pull request by adding 'Area-Containers' repo.

The development documentation is available [here](https://github.com/dotnet/sdk-container-builds/blob/main/docs/DevelopmentDocumentation.md)

When `SOURCE_DATE_EPOCH` is set, container publishing can reuse a deterministic application
layer after validating the source tar fingerprint and the cached compressed and uncompressed
digests. Registry pushes, daemon loads and archives use the same cache. Set
`ContainerLayerNoIncremental=true` to disable application-layer reuse when an image is built.
The separate archive cache can still reuse a complete image archive; disable that with
`ContainerArchiveNoIncremental=true`. Changed source
content, permissions, ownership, container paths, image format or epoch invalidate reuse.
