// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.DotNet.ProjectTools;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal delegate ValueTask ProcessExitAction(int processId, int? exitCode);

internal sealed class ProjectLauncher(
    DotNetWatchContext context,
    LoadedProjectGraph projectGraph,
    RunningProjectsManager runningProjectsManager,
    int iteration)
{
    public int Iteration = iteration;

    public ILogger Logger
        => context.Logger;

    public ILoggerFactory LoggerFactory
        => context.LoggerFactory;

    public EnvironmentOptions EnvironmentOptions
        => context.EnvironmentOptions;

    public RunningProjectsManager RunningProjectsManager
        => runningProjectsManager;

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

        environmentBuilder[EnvironmentVariables.Names.DotnetWatch] = "1";
        environmentBuilder[EnvironmentVariables.Names.DotnetWatchIteration] = (Iteration + 1).ToString(CultureInfo.InvariantCulture);

        if (clients.IsManagedAgentSupported && Logger.IsEnabled(LogLevel.Trace))
        {
            environmentBuilder[EnvironmentVariables.Names.HotReloadDeltaClientLogMessages] =
                (EnvironmentOptions.SuppressEmojis ? Emoji.Default : Emoji.Agent).GetLogMessagePrefix(EnvironmentOptions.LogMessagePrefix) + $"[{projectDisplayName}]";
        }

        // The startup hooks are passed to `dotnet run` via `-e`, which overrides both the value inherited from this process
        // and the value set by the launch profile. Start from the value the application would have received without watch,
        // so that the Hot Reload agent's hook is added to the existing hooks rather than replacing them.
        if (GetApplicationStartupHooks(projectNode, projectOptions) is { Length: > 0 } startupHooks)
        {
            environmentBuilder[EnvironmentVariables.Names.DotNetStartupHooks] = startupHooks;
        }

        clients.ConfigureLaunchEnvironment(environmentBuilder);

        processSpec.Arguments = GetProcessArguments(projectOptions, environmentBuilder);

        // Observes main project process output and launches browser when the URL is found in the output.
        var outputObserver = context.BrowserLauncher.TryGetBrowserLaunchOutputObserver(projectNode, projectOptions, clients.BrowserRefreshServer, cancellationToken);

        processSpec.RedirectOutput(outputObserver, context.ProcessOutputReporter, context.EnvironmentOptions, projectDisplayName);

        return await runningProjectsManager.TrackRunningProjectAsync(
            projectNode,
            projectOptions,
            clients,
            clientLogger,
            processSpec,
            restartOperation,
            cancellationToken);
    }

    /// <summary>
    /// Returns the value of <c>DOTNET_STARTUP_HOOKS</c> that <c>dotnet run</c> would set for the application:
    /// the value defined by the selected launch profile, if any, otherwise the value inherited from this process.
    /// </summary>
    private string? GetApplicationStartupHooks(ProjectGraphNode projectNode, ProjectOptions projectOptions)
    {
        // No value means that no launch profile is used (--no-launch-profile), null means the default profile.
        if (projectOptions.LaunchProfileName.HasValue &&
            ReadLaunchProfile(projectNode, projectOptions.Representation, projectOptions.LaunchProfileName.Value) is { } profile)
        {
            if (profile.EnvironmentVariables.TryGetValue(EnvironmentVariables.Names.DotNetStartupHooks, out var value))
            {
                return value;
            }

            // Environment variable names are case-insensitive on Windows.
            // If the profile defines the variable under more than one spelling, the value `dotnet run` ends up with is unspecified as well.
            if (OperatingSystem.IsWindows())
            {
                foreach (var (name, otherSpellingValue) in profile.EnvironmentVariables)
                {
                    if (string.Equals(name, EnvironmentVariables.Names.DotNetStartupHooks, StringComparison.OrdinalIgnoreCase))
                    {
                        return otherSpellingValue;
                    }
                }
            }
        }

        return EnvironmentOptions.DotNetStartupHooks;
    }

    /// <summary>
    /// Selects and reads the launch profile using the same parser as <c>dotnet run</c>.
    /// Returns null if the profile can't be read. For ordinary parse failures <c>dotnet run</c> reports the problem and proceeds without a profile.
    /// The exceptions caught below only keep dotnet watch running and make the seed fall back to the inherited value;
    /// <c>dotnet run</c> still encounters the same profile content.
    /// </summary>
    /// <remarks>
    /// <c>dotnet run</c> also expands the profile's <c>commandLineArgs</c> when they apply and ignores the whole profile if that expansion fails.
    /// Whether they apply depends on the application arguments and on the project's run arguments, so they are not expanded here.
    /// </remarks>
    private static LaunchProfile? ReadLaunchProfile(ProjectGraphNode projectNode, ProjectRepresentation project, string? launchProfileName)
    {
        var launchSettingsPath = LaunchSettings.TryFindLaunchSettingsFile(project.ProjectOrEntryPointFilePath, launchProfileName, report: static (_, _) => { });
        if (launchSettingsPath == null)
        {
            return null;
        }

        try
        {
            return LaunchSettings.ReadProfileSettingsFromFile(
                launchSettingsPath,
                launchProfileName,
                new LaunchProfileParserOptions(
                    EvaluateExpression: projectNode.ProjectInstance.ExpandString,
                    ExpandProjectProfile: true,
                    ExpandExecutableProfile: true,
                    ExpandCommandLineArgs: false)).Profile;
        }
        catch (Exception e) when (e is InvalidProjectFileException or ArgumentNullException)
        {
            // Invalid MSBuild expression, or a null environment variable value, which the parser passes to Environment.ExpandEnvironmentVariables.
            return null;
        }
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

        if (projectOptions.Device != null)
        {
            arguments.Add("--device");
            arguments.Add(projectOptions.Device);

            if (projectOptions.DeviceRuntimeIdentifier != null)
            {
                arguments.Add("--runtime");
                arguments.Add(projectOptions.DeviceRuntimeIdentifier);
            }
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
