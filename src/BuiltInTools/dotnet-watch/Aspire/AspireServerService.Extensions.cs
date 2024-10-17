// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Watcher;

namespace Microsoft.WebTools.AspireServer;

internal partial class AspireServerService : IRuntimeProcessLauncher
{
    public bool SupportsPartialRestart => false;

    public async ValueTask<IEnumerable<(string name, string value)>> GetEnvironmentVariablesAsync(CancellationToken cancelationToken)
    {
        var environment = await GetServerConnectionEnvironmentAsync(cancelationToken).ConfigureAwait(false);
        return environment.Select(kvp => (kvp.Key, kvp.Value));
    }
}
