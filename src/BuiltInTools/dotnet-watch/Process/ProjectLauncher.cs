// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal delegate ValueTask ProcessExitAction(int processId, int? exitCode);

internal sealed class ProjectLauncher(
    DotNetWatchContext context,
    ProjectNodeMap projectMap,
    BrowserRefreshServerFactory browserConnector,
    BrowserLauncher browserLauncher,
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

    public async ValueTask<RunningProject?> TryLaunchProcessAsync(
        ProjectOptions projectOptions,
        CancellationTokenSource processTerminationSource,
        Action<OutputLine>? onOutput,
        RestartOperation restartOperation,
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
            Logger.LogError($"Hot Reload based watching is only supported in .NET 6.0 or newer apps. Use --no-hot-reload switch or update the project's launchSettings.json to disable this feature.");
            return null;
        }

        var appModel = HotReloadAppModel.InferFromProject(projectNode, Logger);

        var processSpec = new ProcessSpec
        {
            Executable = EnvironmentOptions.MuxerPath,
            IsUserApplication = true,
            WorkingDirectory = projectOptions.WorkingDirectory,
            OnOutput = onOutput,
        };

        // Stream output lines to the process output reporter.
        // The reporter synchronizes the output of the process with the logger output,
        // so that the printed lines don't interleave.
        var projectDisplayName = projectNode.GetDisplayName();
        processSpec.OnOutput += line =>
        {
            context.ProcessOutputReporter.ReportOutput(context.ProcessOutputReporter.PrefixProcessOutput ? line with { Content = $"[{projectDisplayName}] {line.Content}" } : line);
        };

        var environmentBuilder = EnvironmentVariablesBuilder.FromCurrentEnvironment();
        var namedPipeName = Guid.NewGuid().ToString();

        foreach (var (name, value) in projectOptions.LaunchEnvironmentVariables)
        {
            // ignore dotnet-watch reserved variables -- these shouldn't be set by the project
            if (name.Equals(EnvironmentVariables.Names.AspNetCoreHostingStartupAssemblies, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(EnvironmentVariables.Names.DotNetStartupHooks, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            environmentBuilder.SetVariable(name, value);
        }

        // override any project settings:
        environmentBuilder.SetVariable(EnvironmentVariables.Names.DotnetWatch, "1");
        environmentBuilder.SetVariable(EnvironmentVariables.Names.DotnetWatchIteration, (Iteration + 1).ToString(CultureInfo.InvariantCulture));

        // Note:
        // Microsoft.AspNetCore.Components.WebAssembly.Server.ComponentWebAssemblyConventions and Microsoft.AspNetCore.Watch.BrowserRefresh.BrowserRefreshMiddleware
        // expect DOTNET_MODIFIABLE_ASSEMBLIES to be set in the blazor-devserver process, even though we are not performing Hot Reload in this process.
        // The value is converted to DOTNET-MODIFIABLE-ASSEMBLIES header, which is in turn converted back to environment variable in Mono browser runtime loader:
        // https://github.com/dotnet/runtime/blob/342936c5a88653f0f622e9d6cb727a0e59279b31/src/mono/browser/runtime/loader/config.ts#L330
        environmentBuilder.SetVariable(EnvironmentVariables.Names.DotNetModifiableAssemblies, "debug");

        if (appModel.TryGetStartupHookPath(out var startupHookPath))
        {
            // HotReload startup hook should be loaded before any other startup hooks:
            environmentBuilder.DotNetStartupHooks.Insert(0, startupHookPath);

            environmentBuilder.SetVariable(EnvironmentVariables.Names.DotNetWatchHotReloadNamedPipeName, namedPipeName);

            if (context.Options.Verbose)
            {
                environmentBuilder.SetVariable(
                    EnvironmentVariables.Names.HotReloadDeltaClientLogMessages,
                    LoggingUtilities.GetPrefix(EnvironmentOptions.SuppressEmojis ? Emoji.Default : Emoji.Agent) + $"[{projectDisplayName}]");
            }
        }

        var browserRefreshServer = await browserConnector.GetOrCreateBrowserRefreshServerAsync(projectNode, appModel, cancellationToken);
        browserRefreshServer?.SetEnvironmentVariables(environmentBuilder);

        var arguments = new List<string>()
        {
            projectOptions.Command,
            "--no-build"
        };

        foreach (var (name, value) in environmentBuilder.GetEnvironment())
        {
            arguments.Add("-e");
            arguments.Add($"{name}={value}");
        }

        arguments.AddRange(projectOptions.CommandArguments);

        processSpec.Arguments = arguments;

        // Attach trigger to the process that detects when the web server reports to the output that it's listening.
        // Launches browser on the URL found in the process output for root projects.
        browserLauncher.InstallBrowserLaunchTrigger(processSpec, projectNode, projectOptions, browserRefreshServer, cancellationToken);

        return await compilationHandler.TrackRunningProjectAsync(
            projectNode,
            projectOptions,
            appModel,
            namedPipeName,
            browserRefreshServer,
            processSpec,
            restartOperation,
            processTerminationSource,
            cancellationToken);
    }

    public ValueTask<int> TerminateProcessAsync(RunningProject project, CancellationToken cancellationToken)
        => compilationHandler.TerminateNonRootProcessAsync(project, cancellationToken);
}
