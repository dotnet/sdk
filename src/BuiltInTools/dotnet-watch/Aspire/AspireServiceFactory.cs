// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;
using Aspire.Tools.Service;
using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch;

internal class AspireServiceFactory : IRuntimeProcessLauncherFactory
{
    private sealed class SessionManager : IAspireServerEvents, IRuntimeProcessLauncher
    {
        private readonly struct Session(string dcpId, string sessionId, RunningProject runningProject, Task outputReader)
        {
            public string DcpId { get; } = dcpId;
            public string Id { get; } = sessionId;
            public RunningProject RunningProject { get; } = runningProject;
            public Task OutputReader { get; } = outputReader;
        }

        private static readonly UnboundedChannelOptions s_outputChannelOptions = new()
        {
            SingleReader = true,
            SingleWriter = true
        };

        private readonly ProjectLauncher _projectLauncher;
        private readonly AspireServerService _service;
        private readonly IReadOnlyList<string> _buildArguments;

        /// <summary>
        /// Lock to access:
        /// <see cref="_sessions"/>
        /// <see cref="_sessionIdDispenser"/>
        /// </summary>
        private readonly object _guard = new();

        private readonly Dictionary<string, Session> _sessions = [];
        private int _sessionIdDispenser;
        private volatile bool _isDisposed;

        public SessionManager(ProjectLauncher projectLauncher, IReadOnlyList<string> buildArguments)
        {
            _projectLauncher = projectLauncher;
            _buildArguments = buildArguments;

            _service = new AspireServerService(
                this,
                displayName: ".NET Watch Aspire Server",
                m => projectLauncher.Reporter.Verbose(m, MessageEmoji));
        }

        public async ValueTask DisposeAsync()
        {
#if DEBUG
            lock (_guard)
            {
                Debug.Assert(_sessions.Count == 0);
            }
#endif
            _isDisposed = true;

            await _service.DisposeAsync();
        }

        public async ValueTask TerminateLaunchedProcessesAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            ImmutableArray<Session> sessions;
            lock (_guard)
            {
                // caller guarantees the session is active
                sessions = [.. _sessions.Values];
                _sessions.Clear();
            }

            foreach (var session in sessions)
            {
                await TerminateSessionAsync(session, cancellationToken);
            }
        }

        public IEnumerable<(string name, string value)> GetEnvironmentVariables()
            => _service.GetServerConnectionEnvironment().Select(kvp => (kvp.Key, kvp.Value));

        private IReporter Reporter
            => _projectLauncher.Reporter;

        /// <summary>
        /// Implements https://github.com/dotnet/aspire/blob/445d2fc8a6a0b7ce3d8cc42def4d37b02709043b/docs/specs/IDE-execution.md#create-session-request.
        /// </summary>
        async ValueTask<string> IAspireServerEvents.StartProjectAsync(string dcpId, ProjectLaunchRequest projectLaunchInfo, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            var projectOptions = GetProjectOptions(projectLaunchInfo);
            var sessionId = Interlocked.Increment(ref _sessionIdDispenser).ToString(CultureInfo.InvariantCulture);
            await StartProjectAsync(dcpId, sessionId, projectOptions, isRestart: false, cancellationToken);
            return sessionId;
        }

        public async ValueTask<RunningProject> StartProjectAsync(string dcpId, string sessionId, ProjectOptions projectOptions, bool isRestart, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            Reporter.Verbose($"Starting project: {projectOptions.ProjectPath}", MessageEmoji);

            var processTerminationSource = new CancellationTokenSource();
            var outputChannel = Channel.CreateUnbounded<OutputLine>(s_outputChannelOptions);

            var runningProject = await _projectLauncher.TryLaunchProcessAsync(
                projectOptions,
                processTerminationSource,
                onOutput: line =>
                {
                    var writeResult = outputChannel.Writer.TryWrite(line);
                    Debug.Assert(writeResult);
                },
                restartOperation: cancellationToken =>
                    StartProjectAsync(dcpId, sessionId, projectOptions, isRestart: true, cancellationToken),
                cancellationToken);

            if (runningProject == null)
            {
                // detailed error already reported:
                throw new ApplicationException($"Failed to launch project '{projectOptions.ProjectPath}'.");
            }

            await _service.NotifySessionStartedAsync(dcpId, sessionId, runningProject.ProcessId, cancellationToken);

            // cancel reading output when the process terminates:
            var outputReader = StartChannelReader(processTerminationSource.Token);

            lock (_guard)
            {
                // When process is restarted we reuse the session id.
                // The session already exists, it needs to be updated with new info.
                Debug.Assert(_sessions.ContainsKey(sessionId) == isRestart);

                _sessions[sessionId] = new Session(dcpId, sessionId, runningProject, outputReader);
            }

            Reporter.Verbose($"Session started: #{sessionId}", MessageEmoji);
            return runningProject;

            async Task StartChannelReader(CancellationToken cancellationToken)
            {
                try
                {
                    await foreach (var line in outputChannel.Reader.ReadAllAsync(cancellationToken))
                    {
                        await _service.NotifyLogMessageAsync(dcpId, sessionId, isStdErr: line.IsError, data: line.Content, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    // nop
                }
                catch (Exception e)
                {
                    Reporter.Error($"Unexpected error reading output of session '{sessionId}': {e}");
                }
            }
        }

        /// <summary>
        /// Implements https://github.com/dotnet/aspire/blob/445d2fc8a6a0b7ce3d8cc42def4d37b02709043b/docs/specs/IDE-execution.md#stop-session-request.
        /// </summary>
        async ValueTask<bool> IAspireServerEvents.StopSessionAsync(string dcpId, string sessionId, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            Session session;
            lock (_guard)
            {
                if (!_sessions.TryGetValue(sessionId, out session))
                {
                    return false;
                }

                _sessions.Remove(sessionId);
            }

            await TerminateSessionAsync(session, cancellationToken);
            return true;
        }

        private async ValueTask TerminateSessionAsync(Session session, CancellationToken cancellationToken)
        {
            Reporter.Verbose($"Stop session #{session.Id}", MessageEmoji);

            var exitCode = await _projectLauncher.TerminateProcessAsync(session.RunningProject, cancellationToken);

            // Wait until the started notification has been sent so that we don't send out of order notifications:
            await _service.NotifySessionEndedAsync(session.DcpId, session.Id, session.RunningProject.ProcessId, exitCode, cancellationToken);

            // process termination should cancel output reader task:
            await session.OutputReader;
        }

        private ProjectOptions GetProjectOptions(ProjectLaunchRequest projectLaunchInfo)
        {
            var arguments = new List<string>
            {
                "--project",
                projectLaunchInfo.ProjectPath,
                // TODO: https://github.com/dotnet/sdk/issues/43946
                // Need to suppress launch profile for now, otherwise it would override the port set via env variable.
                "--no-launch-profile",
            };

            //if (projectLaunchInfo.DisableLaunchProfile)
            //{
            //    arguments.Add("--no-launch-profile");
            //}
            //else if (!string.IsNullOrEmpty(projectLaunchInfo.LaunchProfile))
            //{
            //    arguments.Add("--launch-profile");
            //    arguments.Add(projectLaunchInfo.LaunchProfile);
            //}

            if (projectLaunchInfo.Arguments != null)
            {
                arguments.AddRange(projectLaunchInfo.Arguments);
            }

            return new()
            {
                IsRootProject = false,
                ProjectPath = projectLaunchInfo.ProjectPath,
                WorkingDirectory = _projectLauncher.EnvironmentOptions.WorkingDirectory, // TODO: Should DCP protocol specify?
                BuildArguments = _buildArguments, // TODO: Should DCP protocol specify?
                Command = "run",
                CommandArguments = arguments,
                LaunchEnvironmentVariables = projectLaunchInfo.Environment?.Select(kvp => (kvp.Key, kvp.Value)).ToArray() ?? [],
                LaunchProfileName = projectLaunchInfo.LaunchProfile,
                NoLaunchProfile = projectLaunchInfo.DisableLaunchProfile,
                TargetFramework = null, // TODO: Should DCP protocol specify?
            };
        }
    }

    public const string MessageEmoji = "⭐";

    public static readonly AspireServiceFactory Instance = new();
    public const string AppHostProjectCapability = "Aspire";

    public IRuntimeProcessLauncher? TryCreate(ProjectGraphNode projectNode, ProjectLauncher projectLauncher, IReadOnlyList<string> buildArguments)
        => projectNode.GetCapabilities().Contains(AppHostProjectCapability)
            ? new SessionManager(projectLauncher, buildArguments)
            : null;
}
