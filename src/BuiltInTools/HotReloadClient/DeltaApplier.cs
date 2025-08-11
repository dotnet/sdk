// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload
{
    internal abstract class DeltaApplier(ILogger logger) : IDisposable
    {
        public readonly ILogger Logger = logger;

        public abstract void CreateConnection(string namedPipeName, CancellationToken cancellationToken);

        /// <summary>
        /// Waits for the application process to start.
        /// Ensures that the build has been complete and the build outputs are available.
        /// </summary>
        public abstract Task WaitForProcessRunningAsync(CancellationToken cancellationToken);

        public abstract Task<ImmutableArray<string>> GetApplyUpdateCapabilitiesAsync(CancellationToken cancellationToken);

        public abstract Task<ApplyStatus> ApplyManagedCodeUpdates(ImmutableArray<HotReloadManagedCodeUpdate> updates, bool isProcessSuspended, CancellationToken cancellationToken);
        public abstract Task<ApplyStatus> ApplyStaticAssetUpdates(ImmutableArray<HotReloadStaticAssetUpdate> updates, bool isProcessSuspended, CancellationToken cancellationToken);

        public abstract Task InitialUpdatesApplied(CancellationToken cancellationToken);

        public abstract void Dispose();

        public static void ReportLogEntry(ILogger logger, string message, AgentMessageSeverity severity)
        {
            var level = severity switch
            {
                AgentMessageSeverity.Error => LogLevel.Error,
                AgentMessageSeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Debug
            };

            logger.Log(level, message);
        }
    }

    internal enum ApplyStatus
    {
        /// <summary>
        /// Failed to apply updates.
        /// </summary>
        Failed = 0,

        /// <summary>
        /// All requested updates have been applied successfully.
        /// </summary>
        AllChangesApplied = 1,

        /// <summary>
        /// Succeeded aplying changes, but some updates were not applicable to the target process because of required capabilities.
        /// </summary>
        SomeChangesApplied = 2,

        /// <summary>
        /// No updates were applicable to the target process because of required capabilities.
        /// </summary>
        NoChangesApplied = 3,
    }
}
