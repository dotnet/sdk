// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Tests;

internal readonly struct SdkDirectoryScope : IDisposable
{
    private readonly object? _previousSdkRoot = AppContext.GetData(SdkPaths.DataName);

    public SdkDirectoryScope(string sdkDirectory)
    {
        AppContext.SetData(SdkPaths.DataName, sdkDirectory);
        SdkPaths.ClearSdkDirectoryCacheForTests();
    }

    public void Dispose()
    {
        AppContext.SetData(SdkPaths.DataName, _previousSdkRoot);
        SdkPaths.ClearSdkDirectoryCacheForTests();
    }
}
