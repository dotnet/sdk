// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Abstract base for interactive selection prompts in dotnet-watch.
/// Provides target framework and device selection with caching across watch restarts.
/// </summary>
internal abstract class BuildParametersSelectionPrompt : IDisposable
{
    public IReadOnlyList<string>? PreviousTargetFrameworks { get; set; }
    public string? PreviousFrameworkSelection { get; set; }

    public IReadOnlyList<DeviceInfo>? PreviousDevices { get; set; }
    public DeviceInfo? PreviousDeviceSelection { get; set; }

    public async ValueTask<string> SelectTargetFrameworkAsync(IReadOnlyList<string> targetFrameworks, CancellationToken cancellationToken)
    {
        var orderedTargetFrameworks = targetFrameworks.Order(StringComparer.OrdinalIgnoreCase).ToArray();

        if (PreviousFrameworkSelection != null && PreviousTargetFrameworks?.SequenceEqual(orderedTargetFrameworks, StringComparer.OrdinalIgnoreCase) == true)
        {
            return PreviousFrameworkSelection;
        }

        PreviousTargetFrameworks = orderedTargetFrameworks;
        PreviousFrameworkSelection = await PromptForTargetFrameworkAsync(targetFrameworks, cancellationToken);
        return PreviousFrameworkSelection;
    }

    public async ValueTask<DeviceInfo> SelectDeviceAsync(IReadOnlyList<DeviceInfo> devices, CancellationToken cancellationToken)
    {
        if (PreviousDeviceSelection != null && PreviousDevices?.SequenceEqual(devices) == true)
        {
            return PreviousDeviceSelection;
        }

        PreviousDevices = devices;
        PreviousDeviceSelection = await PromptForDeviceAsync(devices, cancellationToken);
        return PreviousDeviceSelection;
    }

    protected abstract Task<string> PromptForTargetFrameworkAsync(IReadOnlyList<string> targetFrameworks, CancellationToken cancellationToken);

    protected abstract Task<DeviceInfo> PromptForDeviceAsync(IReadOnlyList<DeviceInfo> devices, CancellationToken cancellationToken);

    public virtual void Dispose() { }
}
