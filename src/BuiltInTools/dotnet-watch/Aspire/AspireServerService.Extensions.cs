// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Watcher;

namespace Microsoft.WebTools.AspireServer;

internal partial class AspireServerService : IRuntimeProcessLauncher
{
    public bool SupportsPartialRestart => false;

    public IEnumerable<(string name, string value)> GetEnvironmentVariables()
        => GetServerConnectionEnvironment().Select(kvp => (kvp.Key, kvp.Value));
}
