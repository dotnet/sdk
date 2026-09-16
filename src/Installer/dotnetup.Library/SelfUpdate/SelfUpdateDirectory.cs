// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32.SafeHandles;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Keeps validated directory handles alive for one filesystem operation.</summary>
internal sealed class SelfUpdateDirectory(List<SafeFileHandle> handles) : IDisposable
{
    private readonly List<SafeFileHandle> _handles = handles;

    public SafeFileHandle Handle => _handles[^1];

    public void Dispose()
    {
        for (var index = _handles.Count - 1; index >= 0; index--)
        {
            _handles[index].Dispose();
        }
    }
}