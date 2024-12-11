// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch;

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

        var profile = HotReloadProfileReader.InferHotReloadProfile(projectNode, Reporter);

        // Blazor WASM does not need dotnet applier as all changes are applied in the browser,
        // the process being launched is a dev server.
        var injectDeltaApplier = profile != HotReloadProfile.BlazorWebAssembly;

        var processSpec = new ProcessSpec
        {
            Executable = EnvironmentOptions.MuxerPath,
            WorkingDirectory = projectOptions.WorkingDirectory,
            OnOutput = onOutput,
            Arguments = [projectOptions.Command, "--no-build", .. projectOptions.CommandArguments]
        };

        var environmentBuilder = EnvironmentVariablesBuilder.FromCurrentEnvironment();
        var namedPipeName = Guid.NewGuid().ToString();

        // Directives:

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

        // Note:
        // Microsoft.AspNetCore.Components.WebAssembly.Server.ComponentWebAssemblyConventions and Microsoft.AspNetCore.Watch.BrowserRefresh.BrowserRefreshMiddleware
        // expect DOTNET_MODIFIABLE_ASSEMBLIES to be set in the blazor-devserver process, even though we are not performing Hot Reload in this process.
        // The value is converted to DOTNET-MODIFIABLE-ASSEMBLIES header, which is in turn converted back to environment variable in Mono browser runtime loader:
        // https://github.com/dotnet/runtime/blob/342936c5a88653f0f622e9d6cb727a0e59279b31/src/mono/browser/runtime/loader/config.ts#L330
        environmentBuilder.SetDirective(EnvironmentVariables.Names.DotnetModifiableAssemblies, "debug");

        if (injectDeltaApplier)
        {
            environmentBuilder.DotNetStartupHookDirective.Add(DeltaApplier.StartupHookPath);
            environmentBuilder.SetDirective(EnvironmentVariables.Names.DotnetWatchHotReloadNamedPipeName, namedPipeName);

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
        }

        var browserRefreshServer = await browserConnector.LaunchOrRefreshBrowserAsync(projectNode, processSpec, environmentBuilder, projectOptions, cancellationToken);
        environmentBuilder.ConfigureProcess(processSpec);

        var processReporter = new ProjectSpecificReporter(projectNode, Reporter);

        return await compilationHandler.TrackRunningProjectAsync(
            projectNode,
            projectOptions,
            profile,
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
