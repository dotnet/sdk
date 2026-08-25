// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.DotNet.Test.MSTest.Utilities;

namespace Microsoft.DotNet.HotReload.UnitTests;

internal sealed class TestHotReloadClient() : HotReloadClient(new TestLogger(), new TestLogger())
{
    public ImmutableArray<HotReloadManagedCodeUpdate> ReceivedUpdates { get; private set; }
    public Func<ImmutableArray<HotReloadManagedCodeUpdate>, Task<bool>> ApplyImpl { get; init; } = _ => Task.FromResult(true);

    public override void ConfigureLaunchEnvironment(IDictionary<string, string> environmentBuilder)
    {
    }

    public override void InitiateConnection(CancellationToken cancellationToken)
    {
    }

    public override Task WaitForConnectionEstablishedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public override Task<ImmutableArray<string>> GetUpdateCapabilitiesAsync(CancellationToken cancellationToken)
        => Task.FromResult(ImmutableArray<string>.Empty);

    public override Task<Task<bool>> ApplyManagedCodeUpdatesAsync(ImmutableArray<HotReloadManagedCodeUpdate> updates, CancellationToken applyOperationCancellationToken, CancellationToken cancellationToken)
    {
        ReceivedUpdates = updates;
        return Task.FromResult(ApplyImpl(updates));
    }

    public override Task<Task<bool>> ApplyStaticAssetUpdatesAsync(ImmutableArray<HotReloadStaticAssetUpdate> updates, CancellationToken applyOperationCancellationToken, CancellationToken cancellationToken)
        => Task.FromResult(Task.FromResult(true));

    public override Task InitialUpdatesAppliedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public override void Dispose()
    {
    }
}
