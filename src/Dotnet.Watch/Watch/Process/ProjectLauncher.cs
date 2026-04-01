// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal delegate ValueTask ProcessExitAction(int processId, int? exitCode);

internal sealed class ProjectLauncher(
    DotNetWatchContext context,
    LoadedProjectGraph projectGraph,
    CompilationHandler compilationHandler,
    int iteration)
{
    public int Iteration = iteration;

    public ILogger Logger
        => context.Logger;

    public ILoggerFactory LoggerFactory
        => context.LoggerFactory;

    public EnvironmentOptions EnvironmentOptions
        => context.EnvironmentOptions;

    public CompilationHandler CompilationHandler
        => compilationHandler;

    public async ValueTask<RunningProject?> TryLaunchProcessAsync(
        ProjectOptions projectOptions,
        Action<OutputLine>? onOutput,
        ProcessExitAction? onExit,
        RestartOperation restartOperation,
        CancellationToken cancellationToken)
    {
        var projectNode = projectGraph.TryGetProjectNode(projectOptions.Representation.ProjectGraphPath, projectOptions.TargetFramework);
        if (projectNode == null)
        {
            // error already reported
            return null;
        }

        // create loggers that include project name in messages:
        var projectDisplayName = projectNode.GetDisplayName();
        var clientLogger = context.LoggerFactory.CreateLogger(HotReloadDotNetWatcher.ClientLogComponentName, projectDisplayName);
        var agentLogger = context.LoggerFactory.CreateLogger(HotReloadDotNetWatcher.AgentLogComponentName, projectDisplayName);

        var appModel = HotReloadAppModel.InferFromProject(context, projectNode);
        var clients = await appModel.CreateClientsAsync(clientLogger, agentLogger, cancellationToken);

        var processSpec = new ProcessSpec
        {
            Executable = EnvironmentOptions.GetMuxerPath(),
            IsUserApplication = true,
            WorkingDirectory = projectOptions.WorkingDirectory,
            OnOutput = onOutput,
            OnExit = onExit,
        };

        var environmentBuilder = new Dictionary<string, string>();

        // initialize with project settings:
        foreach (var (name, value) in projectOptions.LaunchEnvironmentVariables)
        {
            environmentBuilder[name] = value;
        }

        // override any project settings:
        environmentBuilder[EnvironmentVariables.Names.DotnetWatch] = "1";
        environmentBuilder[EnvironmentVariables.Names.DotnetWatchIteration] = (Iteration + 1).ToString(CultureInfo.InvariantCulture);

        if (clients.IsManagedAgentSupported && Logger.IsEnabled(LogLevel.Trace))
        {
            environmentBuilder[EnvironmentVariables.Names.HotReloadDeltaClientLogMessages] =
                (EnvironmentOptions.SuppressEmojis ? Emoji.Default : Emoji.Agent).GetLogMessagePrefix(EnvironmentOptions.LogMessagePrefix) + $"[{projectDisplayName}]";
        }

        clients.ConfigureLaunchEnvironment(environmentBuilder);

        processSpec.Arguments = GetProcessArguments(projectOptions, environmentBuilder);

        // Observes main project process output and launches browser when the URL is found in the output.
        var outputObserver = context.BrowserLauncher.TryGetBrowserLaunchOutputObserver(projectNode, projectOptions, clients.BrowserRefreshServer, cancellationToken);

        processSpec.RedirectOutput(outputObserver, context.ProcessOutputReporter, context.EnvironmentOptions, projectDisplayName);

        return await compilationHandler.TrackRunningProjectAsync(
            projectNode,
            projectOptions,
            clients,
            clientLogger,
            processSpec,
            restartOperation,
            cancellationToken);
    }

    private static IReadOnlyList<string> GetProcessArguments(ProjectOptions projectOptions, IDictionary<string, string> environmentBuilder)
    {
        var arguments = new List<string>()
        {
            projectOptions.Command,
            "--no-build"
        };

        if (projectOptions.TargetFramework != null)
        {
            arguments.Add("--framework");
            arguments.Add(projectOptions.TargetFramework);
        }

        foreach (var (name, value) in environmentBuilder)
        {
            arguments.Add("-e");
            arguments.Add($"{name}={value}");
        }

        arguments.AddRange(projectOptions.CommandArguments);
        return arguments;
    }
}
