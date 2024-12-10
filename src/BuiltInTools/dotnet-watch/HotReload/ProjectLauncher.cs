// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Graph;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher;

internal delegate ValueTask ProcessExitAction(int processId, int? exitCode);

internal sealed class ProjectLauncher(
    DotNetWatchContext context,
    ProjectNodeMap projectMap,
    BrowserConnector browserConnector,
    CompilationHandler compilationHandler,
    int iteration)
{
    public int Iteration = iteration;

    public IReporter Reporter
        => context.Reporter;

    public EnvironmentOptions EnvironmentOptions
        => context.EnvironmentOptions;

    public async ValueTask<RunningProject?> TryLaunchProcessAsync(
        ProjectOptions projectOptions,
        CancellationTokenSource processTerminationSource,
        Action<OutputLine>? onOutput,
        RestartOperation restartOperation,
        bool build,
        CancellationToken cancellationToken)
    {
        var projectNode = projectMap.TryGetProjectNode(projectOptions.ProjectPath, projectOptions.TargetFramework);
        if (projectNode == null)
        {
            // error already reported
            return null;
        }

        if (!projectNode.IsNetCoreApp(Versions.Version6_0))
        {
            Reporter.Error($"Hot Reload based watching is only supported in .NET 6.0 or newer apps. Use --no-hot-reload switch or update the project's launchSettings.json to disable this feature.");
            return null;
        }

        var processSpec = new ProcessSpec
        {
            Executable = EnvironmentOptions.MuxerPath,
            WorkingDirectory = projectOptions.WorkingDirectory,
            OnOutput = onOutput,
            Arguments = build || projectOptions.Command is not ("run" or "test")
                ? [projectOptions.Command, .. projectOptions.CommandArguments]
                : [projectOptions.Command, "--no-build", .. projectOptions.CommandArguments]
        };

        var environmentBuilder = EnvironmentVariablesBuilder.FromCurrentEnvironment();
        var namedPipeName = Guid.NewGuid().ToString();

        // Directives:

        environmentBuilder.DotNetStartupHookDirective.Add(DeltaApplier.StartupHookPath);
        environmentBuilder.SetDirective(EnvironmentVariables.Names.DotnetModifiableAssemblies, "debug");
        environmentBuilder.SetDirective(EnvironmentVariables.Names.DotnetWatchHotReloadNamedPipeName, namedPipeName);

        // Variables:

        foreach (var (name, value) in projectOptions.LaunchEnvironmentVariables)
        {
            // ignore dotnet-watch reserved variables -- these shouldn't be set by the project
            if (name.Equals(EnvironmentVariables.Names.AspNetCoreHostingStartupAssemblies, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(EnvironmentVariables.Names.DotnetStartupHooks, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            environmentBuilder.SetVariable(name, value);
        }

        // override any project settings:
        environmentBuilder.SetVariable(EnvironmentVariables.Names.DotnetWatch, "1");
        environmentBuilder.SetVariable(EnvironmentVariables.Names.DotnetWatchIteration, (Iteration + 1).ToString(CultureInfo.InvariantCulture));

        // Do not ask agent to log to stdout until https://github.com/dotnet/sdk/issues/40484 is fixed.
        // For now we need to set the env variable explicitly when we need to diagnose issue with the agent.
        // Build targets might launch a process and read it's stdout. If the agent is loaded into such process and starts logging
        // to stdout it might interfere with the expected output.
        //if (context.Options.Verbose)
        //{
        //    environmentBuilder.SetVariable(EnvironmentVariables.Names.HotReloadDeltaClientLogMessages, "1");
        //}

        // TODO: workaround for https://github.com/dotnet/sdk/issues/40484
        var targetPath = projectNode.ProjectInstance.GetPropertyValue("RunCommand");
        environmentBuilder.SetVariable(EnvironmentVariables.Names.DotnetWatchHotReloadTargetProcessPath, targetPath);
        Reporter.Verbose($"Target process is '{targetPath}'");

        var browserRefreshServer = await browserConnector.LaunchOrRefreshBrowserAsync(projectNode, processSpec, environmentBuilder, projectOptions, cancellationToken);
        environmentBuilder.ConfigureProcess(processSpec);

        var processReporter = new ProjectSpecificReporter(projectNode, Reporter);

        return await compilationHandler.TrackRunningProjectAsync(
            projectNode,
            projectOptions,
            namedPipeName,
            browserRefreshServer,
            processSpec,
            restartOperation,
            processReporter,
            processTerminationSource,
            cancellationToken);
    }

    public ValueTask<int> TerminateProcessAsync(RunningProject project, CancellationToken cancellationToken)
        => compilationHandler.TerminateNonRootProcessAsync(project, cancellationToken);
}
