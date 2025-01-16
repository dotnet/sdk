// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch
{
    internal delegate ValueTask<RunningProject> RestartOperation(CancellationToken cancellationToken);

    internal sealed class RunningProject(
        ProjectGraphNode projectNode,
        ProjectOptions options,
        EnvironmentOptions environmentOptions,
        DeltaApplier deltaApplier,
        IReporter reporter,
        BrowserRefreshServer? browserRefreshServer,
        Task<int> runningProcess,
        int processId,
        CancellationTokenSource processExitedSource,
        CancellationTokenSource processTerminationSource,
        RestartOperation restartOperation,
        IReadOnlyList<IDisposable> disposables,
        Task<ImmutableArray<string>> capabilityProvider) : IDisposable
    {
        public readonly ProjectGraphNode ProjectNode = projectNode;
        public readonly ProjectOptions Options = options;
        public readonly BrowserRefreshServer? BrowserRefreshServer = browserRefreshServer;
        public readonly DeltaApplier DeltaApplier = deltaApplier;
        public readonly Task<ImmutableArray<string>> CapabilityProvider = capabilityProvider;
        public readonly IReporter Reporter = reporter;
        public readonly Task<int> RunningProcess = runningProcess;
        public readonly int ProcessId = processId;
        public readonly RestartOperation RestartOperation = restartOperation;

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
        }

        public async ValueTask<int> TerminateAsync(CancellationToken shutdownCancellationToken)
        {
            if (shutdownCancellationToken.IsCancellationRequested)
            {
                // Ctrl+C sent, wait for the process to exit
                try
                {
                    _ = await RunningProcess.WaitAsync(environmentOptions.ProcessCleanupTimeout, CancellationToken.None);
                }
                catch (TimeoutException)
                {
                    // nop
                }
            }

            ProcessTerminationSource.Cancel();
            return await RunningProcess;
        }
    }
}
