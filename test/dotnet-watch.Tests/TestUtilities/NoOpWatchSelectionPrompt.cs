// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

/// <summary>
/// A no-op selection prompt for tests that don't exercise interactive selection.
/// </summary>
internal sealed class NoOpWatchSelectionPrompt : WatchSelectionPrompt
{
    protected override Task<string> PromptForTargetFrameworkAsync(IReadOnlyList<string> targetFrameworks, CancellationToken cancellationToken)
        => Task.FromResult(targetFrameworks[0]);

    protected override Task<DeviceInfo> PromptForDeviceAsync(IReadOnlyList<DeviceInfo> devices, CancellationToken cancellationToken)
        => Task.FromResult(devices[0]);
}
