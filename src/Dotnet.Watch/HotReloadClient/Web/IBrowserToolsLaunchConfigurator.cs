// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.HotReload;

internal interface IBrowserToolsLaunchConfigurator
{
    void ConfigureLaunchEnvironment(IDictionary<string, string> environment);
}

[Flags]
internal enum BrowserToolsLaunchFeatures
{
    None = 0,
    BrowserRefresh = 1,
    ManagedHotReload = 2
}
