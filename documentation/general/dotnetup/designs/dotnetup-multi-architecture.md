### Cross Architecture Considerations

One scenario our admin installers support is installing the x64 .NET SDK on an ARM64 device.
We should consider how to handle these hives in `dotnetup`.

When making this implementation, please also consider whether we should set `DOTNET_ROOT_x64` with `dotnetup dotnet <>` as defined by [`the dotnetup dotnet command design`](./dotnetup-dotnet.md).
