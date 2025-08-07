// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal abstract class DeltaApplier(IReporter reporter) : IDisposable
    {
        public readonly IReporter Reporter = reporter;

        public static readonly string StartupHookPath = Path.Combine(AppContext.BaseDirectory, "hotreload", "Microsoft.Extensions.DotNetDeltaApplier.dll");

        public abstract void CreateConnection(string namedPipeName, CancellationToken cancellationToken);

        /// <summary>
        /// Waits for the application process to start.
        /// Ensures that the build has been complete and the build outputs are available.
        /// </summary>
        public abstract Task WaitForProcessRunningAsync(CancellationToken cancellationToken);

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
