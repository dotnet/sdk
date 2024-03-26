// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal abstract class DeltaApplier : IDisposable
    {
        public abstract void Initialize(ProjectInfo project, string namedPipeName, CancellationToken cancellationToken);

        public abstract Task<ImmutableArray<string>> GetApplyUpdateCapabilitiesAsync(CancellationToken cancellationToken);

        public abstract Task<ApplyStatus> Apply(ImmutableArray<WatchHotReloadService.Update> updates, CancellationToken cancellationToken);

        public abstract void Dispose();
    }

    internal enum ApplyStatus
    {
        Failed = 0,
        AllChangesApplied = 1,
        SomeChangesApplied = 2,
        NoChangesApplied = 3,
    }
}
