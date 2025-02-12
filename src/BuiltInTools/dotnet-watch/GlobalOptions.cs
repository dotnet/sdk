// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher;

internal sealed class GlobalOptions
{
    public bool Quiet { get; init; }
    public bool Verbose { get; init; }
    public bool NoHotReload { get; init; }
    public bool NonInteractive { get; init; }
}
