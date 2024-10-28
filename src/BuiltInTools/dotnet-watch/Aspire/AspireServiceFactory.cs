// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Graph;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;
using Microsoft.WebTools.AspireServer;
using Microsoft.WebTools.AspireServer.Contracts;

namespace Microsoft.DotNet.Watcher;

internal class AspireServiceFactory : IRuntimeProcessLauncherFactory
{
    private sealed class ServerEvents(ProjectLauncher projectLauncher, IReadOnlyList<(string name, string value)> buildProperties) : IAspireServerEvents
    {
        /// <summary>
        /// Lock to access:
        /// <see cref="_sessions"/>
        /// <see cref="_sessionIdDispenser"/>
        /// </summary>
        private readonly object _guard = new();

        private readonly Dictionary<string, RunningProject> _sessions = [];
        private int _sessionIdDispenser;

        private IReporter Reporter
            => projectLauncher.Reporter;

        /// <summary>
        /// Implements https://github.com/dotnet/aspire/blob/445d2fc8a6a0b7ce3d8cc42def4d37b02709043b/docs/specs/IDE-execution.md#create-session-request.
        /// </summary>
        public async ValueTask<string> StartProjectAsync(string dcpId, ProjectLaunchRequest projectLaunchInfo, CancellationToken cancellationToken)
        {
            Reporter.Verbose($"Starting project: {projectLaunchInfo.ProjectPath}", MessageEmoji);

            var projectOptions = GetProjectOptions(projectLaunchInfo);

            var processTerminationSource = new CancellationTokenSource();

            var runningProject = await projectLauncher.TryLaunchProcessAsync(projectOptions, processTerminationSource, build: false, cancellationToken);
            if (runningProject == null)
            {
                // detailed error already reported:
                throw new ApplicationException($"Failed to launch project '{projectLaunchInfo.ProjectPath}'.");
            }

            string sessionId;
            lock (_guard)
            {
                sessionId = _sessionIdDispenser++.ToString(CultureInfo.InvariantCulture);
                _sessions.Add(sessionId, runningProject);
            }

            Reporter.Verbose($"Session started: {sessionId}");
            return sessionId;
        }

        /// <summary>
        /// Implements https://github.com/dotnet/aspire/blob/445d2fc8a6a0b7ce3d8cc42def4d37b02709043b/docs/specs/IDE-execution.md#stop-session-request.
        /// </summary>
        public async ValueTask StopSessionAsync(string dcpId, string sessionId, CancellationToken cancellationToken)
        {
            Reporter.Verbose($"Stop Session {sessionId}", MessageEmoji);

            RunningProject? runningProject;
            lock (_guard)
            {
                runningProject = _sessions[sessionId];
                _sessions.Remove(sessionId);
            }

            _ = await projectLauncher.TerminateProcessesAsync([runningProject.ProjectNode.ProjectInstance.FullPath], cancellationToken);
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
                WorkingDirectory = projectLauncher.EnvironmentOptions.WorkingDirectory, // TODO: Should DCP protocol specify?
                BuildProperties = buildProperties, // TODO: Should DCP protocol specify?
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

    public IRuntimeProcessLauncher? TryCreate(ProjectGraphNode projectNode, ProjectLauncher projectLauncher, IReadOnlyList<(string name, string value)> buildProperties)
    {
        if (!projectNode.GetCapabilities().Contains(AppHostProjectCapability))
        {
            return null;
        }

        // TODO: implement notifications:
        // 1) Process restarted notification
        // 2) Session terminated notification
        return new AspireServerService(new ServerEvents(projectLauncher, buildProperties), displayName: ".NET Watch Aspire Server", m => projectLauncher.Reporter.Verbose(m, MessageEmoji));
    }
}
