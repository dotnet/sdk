// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// Information 
/// </summary>
internal readonly struct HotReloadAgentInfo
{
    internal static readonly HotReloadAgentInfo Unavailable = new() { ManagedCodeUpdateCapabilities = [] };

    /// <summary>
    /// Capabilities of the runtime the agent is loaded to.
    /// </summary>
    public required ImmutableArray<string> ManagedCodeUpdateCapabilities { get; init; }

    /// <summary>
    /// The id of the process the agent is loaded to, if available and the agent runs on the same machine as the client.
    /// Null if the agent runs on a device or in the browser.
    /// </summary>
    public int? LocalProcessId { get; init; }
}
