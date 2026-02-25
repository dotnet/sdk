// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch.UnitTests;

internal class TestHotReloadAgent : IHotReloadAgent
{
    public Action<IEnumerable<RuntimeManagedCodeUpdate>>? ApplyManagedCodeUpdatesImpl = null;
    public Action<RuntimeStaticAssetUpdate>? ApplyStaticAssetUpdateImpl = null;

    public AgentReporter Reporter { get; set; } = new();
    public string Capabilities { get; set; } = "Baseline";

    public void ApplyManagedCodeUpdates(IEnumerable<RuntimeManagedCodeUpdate> updates)
        => ApplyManagedCodeUpdatesImpl?.Invoke(updates);

    public void ApplyStaticAssetUpdate(RuntimeStaticAssetUpdate update)
        => ApplyStaticAssetUpdateImpl?.Invoke(update);

    public void Dispose()
    {
    }
}
