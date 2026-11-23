// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>Allocates progress task IDs that are unique across every progress target sharing this instance.</summary>
public sealed class ProgressTaskIds
{
    private int _lastId;

    public int Next() => Interlocked.Increment(ref _lastId);
}
