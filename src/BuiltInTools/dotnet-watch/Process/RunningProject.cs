// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch
{
    internal delegate ValueTask<RunningProject> RestartOperation(CancellationToken cancellationToken);

    internal sealed class RunningProject(
        ProjectGraphNode projectNode,
        ProjectOptions options,
        HotReloadClients clients,
        Task<int> runningProcess,
        int processId,
        CancellationTokenSource processExitedSource,
        CancellationTokenSource processTerminationSource,
        RestartOperation restartOperation,
        ImmutableArray<string> capabilities) : IDisposable
    {
        public readonly ProjectGraphNode ProjectNode = projectNode;
        public readonly ProjectOptions Options = options;
        public readonly HotReloadClients Clients = clients;
        public readonly ImmutableArray<string> Capabilities = capabilities;
        public readonly Task<int> RunningProcess = runningProcess;
        public readonly int ProcessId = processId;
        public readonly RestartOperation RestartOperation = restartOperation;

        /// <summary>
        /// Cancellation token triggered when the process exits.
        /// Stores the token to allow callers to use the token even after the source has been disposed.
        /// </summary>
        public CancellationToken ProcessExitedCancellationToken = processExitedSource.Token;

        /// <summary>
        /// Set to true when the process termination is being requested so that it can be restarted within
        /// the Hot Reload session (i.e. without restarting the root project).
        /// </summary>
        public bool IsRestarting { get; private set; }

        private volatile bool _isDisposed;

        /// <summary>
        /// Disposes the project. Can occur unexpectedly whenever the process exits.
        /// Must only be called once per project.
        /// </summary>
        public void Dispose()
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _isDisposed = true;
            processExitedSource.Cancel();

            Clients.Dispose();
            processTerminationSource.Dispose();
            processExitedSource.Dispose();
        }

        /// <summary>
        /// Waits for the application process to start.
        /// Ensures that the build has been complete and the build outputs are available.
        /// Returns false if the process has exited before the connection was established.
        /// </summary>
        public async ValueTask<bool> WaitForProcessRunningAsync(CancellationToken cancellationToken)
        {
            using var processCommunicationCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ProcessExitedCancellationToken);

            try
            {
                await Clients.WaitForConnectionEstablishedAsync(processCommunicationCancellationSource.Token);
                return true;
            }
            catch (OperationCanceledException) when (ProcessExitedCancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        public async Task<int> TerminateAsync(bool isRestarting)
        {
            IsRestarting = isRestarting;

            if (!_isDisposed)
            {
                processTerminationSource.Cancel();
            }

            return await RunningProcess;
        }
    }
}
