// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;
using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch
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

        public abstract Task<ApplyStatus> ApplyManagedCodeUpdates(ImmutableArray<WatchHotReloadService.Update> updates, CancellationToken cancellationToken);
        public abstract Task<ApplyStatus> ApplyStaticAssetUpdates(ImmutableArray<StaticAssetUpdate> updates, CancellationToken cancellationToken);

        public abstract Task InitialUpdatesApplied(CancellationToken cancellationToken);
        public abstract Task<bool> TryTerminateProcessAsync(CancellationToken cancellationToken);

        public abstract void Dispose();

        public static void ReportLogEntry(IReporter reporter, string message, AgentMessageSeverity severity)
        {
            switch (severity)
            {
                case AgentMessageSeverity.Error:
                    reporter.Error(message);
                    break;

                case AgentMessageSeverity.Warning:
                    reporter.Warn(message, emoji: "⚠");
                    break;

                default:
                    reporter.Verbose(message, emoji: "🕵️");
                    break;
            }
        }
    }

    internal enum ApplyStatus
    {
        Failed = 0,
        AllChangesApplied = 1,
        SomeChangesApplied = 2,
        NoChangesApplied = 3,
    }
}
