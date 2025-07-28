// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.DotNet.Watch;

internal readonly struct HotReloadManagedCodeUpdate(
    Guid moduleId,
    ImmutableArray<byte> metadataDelta,
    ImmutableArray<byte> ilDelta,
    ImmutableArray<byte> pdbDelta,
    ImmutableArray<int> updatedTypes,
    ImmutableArray<string> requiredCapabilities)
{
    public Guid ModuleId { get; } = moduleId;
    public ImmutableArray<byte> MetadataDelta { get; } = metadataDelta;
    public ImmutableArray<byte> ILDelta { get; } = ilDelta;
    public ImmutableArray<byte> PdbDelta { get; } = pdbDelta;
    public ImmutableArray<int> UpdatedTypes { get; } = updatedTypes;
    public ImmutableArray<string> RequiredCapabilities { get; } = requiredCapabilities;
}
