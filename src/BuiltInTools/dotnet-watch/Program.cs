// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Runtime.Loader;
using Microsoft.Build.Graph;
using Microsoft.Build.Locator;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;
using IConsole = Microsoft.Extensions.Tools.Internal.IConsole;

namespace Microsoft.DotNet.Watcher
{
    internal sealed class Program(IConsole console, IReporter reporter, CommandLineOptions options, EnvironmentOptions environmentOptions)
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                var sdkRootDirectory = EnvironmentVariables.SdkRootDirectory;

                // We can register the MSBuild that is bundled with the SDK to perform MSBuild things.
                // In production deployment dotnet-watch is in a nested folder of the SDK's root, we'll back up to it.
                // AppContext.BaseDirectory = $sdkRoot\$sdkVersion\DotnetTools\dotnet-watch\$version\tools\net6.0\any\
                // MSBuild.dll is located at $sdkRoot\$sdkVersion\MSBuild.dll
                if (string.IsNullOrEmpty(sdkRootDirectory))
                {
                    sdkRootDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..");
                }

                MSBuildLocator.RegisterMSBuildPath(sdkRootDirectory);

                // Register listeners that load Roslyn-related assemblies from the `Roslyn/bincore` directory.
                RegisterAssemblyResolutionEvents(sdkRootDirectory);

                var console = PhysicalConsole.Singleton;
                var verbose = EnvironmentVariables.VerboseCliOutput;
                var environmentOptions = EnvironmentOptions.FromEnvironment();

                var options = CommandLineOptions.Parse(args, new ConsoleReporter(console, verbose, quiet: false, environmentOptions.SuppressEmojis), out var errorCode);
                if (options == null)
                {
                    // an error reported or help printed:
                    return errorCode;
                }

                var reporter = new ConsoleReporter(console, verbose || options.Verbose, options.Quiet, environmentOptions.SuppressEmojis);
                var program = new Program(console, reporter, options, environmentOptions);

                return await program.RunAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Unexpected error:");
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        internal async Task<int> RunAsync()
        {
            var cancellationSource = new CancellationTokenSource();
            var cancellationToken = cancellationSource.Token;
            console.CancelKeyPress += OnCancelKeyPress;

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return 1;
                }

                if (options.List)
                {
                    return await ListFilesAsync(cancellationToken);
                }
                else
                {
                    return await RunAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is OperationCanceledException)
                {
                    // swallow when only exception is the CTRL+C forced an exit
                    return 0;
                }

                reporter.Error(ex.ToString());
                reporter.Error("An unexpected error occurred");
                return 1;
            }
            finally
            {
                console.CancelKeyPress -= OnCancelKeyPress;
                cancellationSource.Dispose();
            }

            void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
            {
                // suppress CTRL+C on the first press
                args.Cancel = !cancellationSource.IsCancellationRequested;

                if (args.Cancel)
                {
                    reporter.Output("Shutdown requested. Press Ctrl+C again to force exit.", emoji: "🛑");
                }

                cancellationSource.Cancel();
            }
        }

        private async Task<int> RunAsync(CancellationToken cancellationToken)
        {
            // TODO multiple projects should be easy enough to add here
            string projectFile;
            try
            {
                projectFile = MsBuildProjectFinder.FindMsBuildProject(environmentOptions.WorkingDirectory, options.Project);
            }
            catch (FileNotFoundException ex)
            {
                reporter.Error(ex.Message);
                return 1;
            }

            var fileSetFactory = new MsBuildFileSetFactory(
                environmentOptions,
                reporter,
                projectFile,
                options.TargetFramework,
                options.BuildProperties,
                outputSink: null,
                trace: true);

            if (EnvironmentVariables.IsPollingEnabled)
            {
                reporter.Output("Polling file watcher is enabled");
            }

            var projectDirectory = Path.GetDirectoryName(projectFile);
            Debug.Assert(projectDirectory != null);

            var projectGraph = TryReadProject(projectFile, options);

            bool enableHotReload;
            if (options.Command != "run")
            {
                reporter.Verbose($"Command '{options.Command}' does not support Hot Reload.");
                enableHotReload = false;
            }
            else if (options.NoHotReload)
            {
                reporter.Verbose("Hot Reload disabled by command line switch.");
                enableHotReload = false;
            }
            else if (projectGraph is null || !IsHotReloadSupported(projectGraph))
            {
                reporter.Verbose("Project does not support Hot Reload.");
                enableHotReload = false;
            }
            else
            {
                reporter.Verbose("Watching with Hot Reload.");
                enableHotReload = true;
            }

            var args = options.GetLaunchProcessArguments(enableHotReload, reporter, out var noLaunchProfile, out var launchProfileName);
            var launchProfile = (noLaunchProfile ? null : LaunchSettingsProfile.ReadLaunchProfile(projectDirectory, launchProfileName, reporter)) ?? new();

            // If no args forwarded to the app were specified use the ones in the profile.
            var escapedArgs = (enableHotReload && args is []) ? launchProfile.CommandLineArgs : null;

            var context = new DotNetWatchContext
            {
                ProjectGraph = projectGraph,
                Reporter = reporter,
                LaunchSettingsProfile = launchProfile,
                Options = options,
                EnvironmentOptions = environmentOptions,
            };

            var processSpec = new ProcessSpec
            {
                WorkingDirectory = projectDirectory,
                Arguments = args,
                EscapedArguments = escapedArgs,
                EnvironmentVariables =
                {
                    [EnvironmentVariables.Names.DotnetWatch] = "1",
                    [EnvironmentVariables.Names.DotnetLaunchProfile] = launchProfile.LaunchProfileName ?? string.Empty
                }
            };

            if (enableHotReload)
            {
                var watcher = new HotReloadDotNetWatcher(context, console, fileSetFactory);
                await watcher.WatchAsync(processSpec, cancellationToken);
            }
            else
            {
                var watcher = new DotNetWatcher(context, fileSetFactory);
                await watcher.WatchAsync(processSpec, cancellationToken);
            }

            return 0;
        }

        private ProjectGraph? TryReadProject(string project, CommandLineOptions options)
        {
            var globalOptions = new Dictionary<string, string>();
            if (options.TargetFramework != null)
            {
                globalOptions.Add("TargetFramework", options.TargetFramework);
            }

            if (options.BuildProperties != null)
            {
                foreach (var (name, value) in options.BuildProperties)
                {
                    globalOptions[name] = value;
                }
            }

            try
            {
                return new ProjectGraph(project, globalOptions);
            }
            catch (Exception ex)
            {
                reporter.Verbose("Reading the project instance failed.");
                reporter.Verbose(ex.ToString());
            }

            return null;
        }

        private static bool IsHotReloadSupported(ProjectGraph projectGraph)
        {
            var projectInstance = projectGraph.EntryPointNodes.FirstOrDefault()?.ProjectInstance;
            if (projectInstance is null)
            {
                return false;
            }

            var projectCapabilities = projectInstance.GetItems("ProjectCapability");
            foreach (var item in projectCapabilities)
            {
                if (item.EvaluatedInclude == "SupportsHotReload")
                {
                    return true;
                }
            }
            return false;
        }

        private async Task<int> ListFilesAsync(CancellationToken cancellationToken)
        {
            // TODO multiple projects should be easy enough to add here
            string projectFile;
            try
            {
                projectFile = MsBuildProjectFinder.FindMsBuildProject(environmentOptions.WorkingDirectory, options.Project);
            }
            catch (FileNotFoundException ex)
            {
                reporter.Error(ex.Message);
                return 1;
            }

            var fileSetFactory = new MsBuildFileSetFactory(
                environmentOptions,
                reporter,
                projectFile,
                options.TargetFramework,
                options.BuildProperties,
                outputSink: null,
                trace: false);

            if (await fileSetFactory.TryCreateAsync(cancellationToken) is not (_, var files))
            {
                return 1;
            }

            foreach (var file in files)
            {
                console.Out.WriteLine(file.FilePath);
            }

            return 0;
        }

        private static void RegisterAssemblyResolutionEvents(string sdkRootDirectory)
        {
            var roslynPath = Path.Combine(sdkRootDirectory, "Roslyn", "bincore");

            AssemblyLoadContext.Default.Resolving += (context, assembly) =>
            {
                if (assembly.Name is "Microsoft.CodeAnalysis" or "Microsoft.CodeAnalysis.CSharp")
                {
                    var loadedAssembly = context.LoadFromAssemblyPath(Path.Combine(roslynPath, assembly.Name + ".dll"));
                    // Avoid scenarios where the assembly in rosylnPath is older than what we expect
                    if (loadedAssembly.GetName().Version < assembly.Version)
                    {
                        throw new Exception($"Found a version of {assembly.Name} that was lower than the target version of {assembly.Version}");
                    }
                    return loadedAssembly;
                }
                return null;
            };
        }
    }
}
