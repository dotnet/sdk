// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

public class ConformanceLayerFixture : IDisposable
{
    private readonly TransientTestFolderFixture folder = new();
    private Lazy<Layer>? lazyLayer;
    public Layer Layer => lazyLayer?.Value
        ?? throw new InvalidOperationException("Layer has not been initialized.");

    public ConformanceLayerFixture()
    {
        lazyLayer = new Lazy<Layer>(() => Data.Layer.CreateConformanceLayer(folder.TestFolder).GetAwaiter().GetResult(), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public void Dispose()
    {
        lazyLayer = null;
        folder.Dispose();
    }
}
