// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Graph;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher;

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

    public async ValueTask<RunningProject?> TryLaunchProcessAsync(ProjectOptions projectOptions, CancellationTokenSource processTerminationSource, bool build, CancellationToken cancellationToken)
    {
        var projectNode = projectMap.TryGetProjectNode(projectOptions.ProjectPath, projectOptions.TargetFramework);
        if (projectNode == null)
        {
            // error already reported
            return null;
        }

        if (!projectNode.IsNetCoreApp(Versions.Version6_0))
        {
            Reporter.Error($"Hot Reload based watching is only supported in .NET 6.0 or newer apps. Update the project's launchSettings.json to disable this feature.");
            return null;
        }

        try
        {
            return await LaunchProcessAsync(projectOptions, projectNode, processTerminationSource, build, cancellationToken);
        }
        catch (ObjectDisposedException e) when (e.ObjectName == typeof(HotReloadDotNetWatcher).FullName)
        {
            Reporter.Verbose("Unable to launch project, watcher has been disposed");
            return null;
        }
    }

    public async Task<RunningProject> LaunchProcessAsync(ProjectOptions projectOptions, ProjectGraphNode projectNode, CancellationTokenSource processTerminationSource, bool build, CancellationToken cancellationToken)
    {
        var processSpec = new ProcessSpec
        {
            Executable = EnvironmentOptions.MuxerPath,
            WorkingDirectory = projectOptions.WorkingDirectory,
            Arguments = build || projectOptions.Command is not ("run" or "test")
                ? [projectOptions.Command, .. projectOptions.CommandArguments]
                : [projectOptions.Command, "--no-build", .. projectOptions.CommandArguments]
        };

        // allow tests to watch for application output:
        if (Reporter.ReportProcessOutput)
        {
            var projectPath = projectNode.ProjectInstance.FullPath;
            processSpec.OnOutput += (sender, args) =>
            {
                if (args.Data != null)
                {
                    Reporter.ProcessOutput(projectPath, args.Data);
                }
            };
        }

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

        if (context.Options.Verbose)
        {
            environmentBuilder.SetVariable(EnvironmentVariables.Names.HotReloadDeltaClientLogMessages, "1");
        }

        // TODO: workaround for https://github.com/dotnet/sdk/issues/40484
        var targetPath = projectNode.ProjectInstance.GetPropertyValue("RunCommand");
        environmentBuilder.SetVariable(EnvironmentVariables.Names.DotnetWatchHotReloadTargetProcessPath, targetPath);
        Reporter.Verbose($"Target process is '{targetPath}'");

        var browserRefreshServer = await browserConnector.LaunchOrRefreshBrowserAsync(projectNode, processSpec, environmentBuilder, projectOptions, cancellationToken);
        environmentBuilder.ConfigureProcess(processSpec);

        var processReporter = new MessagePrefixingReporter($"[{projectNode.GetDisplayName()}] ", Reporter);

        return await compilationHandler.TrackRunningProjectAsync(
            projectNode,
            projectOptions,
            namedPipeName,
            browserRefreshServer,
            processSpec,
            processReporter,
            processTerminationSource,
            cancellationToken);
    }

    public ValueTask<IEnumerable<RunningProject>> TerminateProcessesAsync(IReadOnlyList<string> projectPaths, CancellationToken cancellationToken)
        => compilationHandler.TerminateNonRootProcessesAsync(projectPaths, cancellationToken);
}
