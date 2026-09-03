// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Immutable;

namespace Microsoft.DotNet.HotReload;

internal sealed record BrowserToolsManagedCodeUpdate(
    Guid ModuleId,
    byte[] MetadataDelta,
    byte[] ILDelta,
    byte[] PdbDelta,
    int[] UpdatedTypes);

internal sealed record BrowserToolsUpdateBatch(
    Guid GenerationId,
    int UpdateId,
    ImmutableArray<BrowserToolsManagedCodeUpdate> Deltas);

internal readonly record struct BrowserToolsReplayResult(
    BrowserToolsReplayStatus Status,
    ImmutableArray<BrowserToolsUpdateBatch> Updates);

internal enum BrowserToolsReplayStatus
{
    CurrentGeneration,
    GenerationMismatch
}

internal sealed class BrowserToolsUpdateStore
{
    private readonly object _guard = new();
    private Guid _generationId = Guid.NewGuid();
    private ImmutableArray<BrowserToolsUpdateBatch> _updates = [];

    public Guid GenerationId
    {
        get
        {
            lock (_guard)
            {
                return _generationId;
            }
        }
    }

    public BrowserToolsReplayResult GetReplay(Guid generationId)
    {
        lock (_guard)
        {
            return generationId == _generationId
                ? new(BrowserToolsReplayStatus.CurrentGeneration, _updates)
                : new(BrowserToolsReplayStatus.GenerationMismatch, []);
        }
    }

    public void Append(BrowserToolsUpdateBatch batch)
    {
        lock (_guard)
        {
            if (batch.GenerationId != _generationId)
            {
                throw new InvalidOperationException("The update batch does not belong to the current application generation.");
            }

            if (_updates is [.., var last] && batch.UpdateId <= last.UpdateId)
            {
                throw new InvalidOperationException("The update batch is not newer than the last retained batch.");
            }

            _updates = _updates.Add(batch);
        }
    }

    public Guid Reset()
    {
        lock (_guard)
        {
            _generationId = Guid.NewGuid();
            _updates = [];
            return _generationId;
        }
    }
}
