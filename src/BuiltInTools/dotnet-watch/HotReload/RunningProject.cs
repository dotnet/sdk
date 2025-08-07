﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class RunningProject(
        ProjectGraphNode projectNode,
        ProjectOptions options,
        DeltaApplier deltaApplier,
        IReporter reporter,
        BrowserRefreshServer? browserRefreshServer,
        Task runningProcess,
        CancellationTokenSource processExitedSource,
        CancellationTokenSource processTerminationSource,
        IReadOnlyList<IDisposable> disposables,
        Task<ImmutableArray<string>> capabilityProvider) : IDisposable
    {
        public readonly ProjectGraphNode ProjectNode = projectNode;
        public readonly ProjectOptions Options = options;
        public readonly BrowserRefreshServer? BrowserRefreshServer = browserRefreshServer;
        public readonly DeltaApplier DeltaApplier = deltaApplier;
        public readonly Task<ImmutableArray<string>> CapabilityProvider = capabilityProvider;
        public readonly IReporter Reporter = reporter;
        public readonly Task RunningProcess = runningProcess;

        /// <summary>
        /// Cancellation source triggered when the process exits.
        /// </summary>
        public readonly CancellationTokenSource ProcessExitedSource = processExitedSource;

        /// <summary>
        /// Cancellation source to use to terminate the process.
        /// </summary>
        public readonly CancellationTokenSource ProcessTerminationSource = processTerminationSource;

        /// <summary>
        /// Misc disposable object to dispose when the object is disposed.
        /// </summary>
        private readonly IReadOnlyList<IDisposable> _disposables = disposables;

        public void Dispose()
        {
            DeltaApplier.Dispose();
            ProcessTerminationSource.Dispose();
            ProcessExitedSource.Dispose();

            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
        }

        /// <summary>
        /// Waits for the application process to start.
        /// Ensures that the build has been complete and the build outputs are available.
        /// </summary>
        public async ValueTask WaitForProcessRunningAsync(CancellationToken cancellationToken)
        {
            await DeltaApplier.WaitForProcessRunningAsync(cancellationToken);
            Reporter.Report(MessageDescriptor.BuildCompleted);
        }
    }
}
