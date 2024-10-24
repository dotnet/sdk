// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics.CodeAnalysis;
using System.Runtime.Loader;
using Microsoft.Build.Graph;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.ChangeSignature;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;
using IConsole = Microsoft.Extensions.Tools.Internal.IConsole;

namespace Microsoft.DotNet.Watcher
{
    internal sealed class Program(IConsole console, IReporter reporter, ProjectOptions rootProjectOptions, CommandLineOptions options, EnvironmentOptions environmentOptions)
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

                var program = TryCreate(
                    args,
                    PhysicalConsole.Singleton,
                    EnvironmentOptions.FromEnvironment(),
                    EnvironmentVariables.VerboseCliOutput,
                    out var exitCode);

                if (program == null)
                {
                    return exitCode;
                }

                return await program.RunAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Unexpected error:");
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static Program? TryCreate(IReadOnlyList<string> args, IConsole console, EnvironmentOptions environmentOptions, bool verbose, out int errorCode)
        {
            var options = CommandLineOptions.Parse(args, new ConsoleReporter(console, verbose, quiet: false, environmentOptions.SuppressEmojis), console.Out, out errorCode);
            if (options == null)
            {
                // an error reported or help printed:
                return null;
            }

            var reporter = new ConsoleReporter(console, verbose || options.GlobalOptions.Verbose, options.GlobalOptions.Quiet, environmentOptions.SuppressEmojis);
            return TryCreate(options, console, environmentOptions, reporter, out errorCode);
        }

        // internal for testing
        internal static Program? TryCreate(CommandLineOptions options, IConsole console, EnvironmentOptions environmentOptions, IReporter reporter, out int errorCode)
        {
            var workingDirectory = environmentOptions.WorkingDirectory;
            reporter.Verbose($"Working directory: '{workingDirectory}'");

            string projectPath;
            try
            {
                projectPath = MsBuildProjectFinder.FindMsBuildProject(workingDirectory, options.ProjectPath);
            }
            catch (FileNotFoundException ex)
            {
                reporter.Error(ex.Message);
                errorCode = 1;
                return null;
            }

            var rootProjectOptions = options.GetProjectOptions(projectPath, workingDirectory);
            errorCode = 0;
            return new Program(console, reporter, rootProjectOptions, options, environmentOptions);
        }

        // internal for testing
        internal async Task<int> RunAsync()
        {
            var shutdownCancellationSource = new CancellationTokenSource();
            var shutdownCancellationToken = shutdownCancellationSource.Token;
            console.CancelKeyPress += OnCancelKeyPress;

            try
            {
                if (shutdownCancellationToken.IsCancellationRequested)
                {
                    return 1;
                }

                if (options.List)
                {
                    return await ListFilesAsync(shutdownCancellationToken);
                }

                var watcher = CreateWatcher(runtimeProcessLauncherFactory: null);
                await watcher.WatchAsync(shutdownCancellationToken);
                return 0;
            }
            catch (OperationCanceledException) when (shutdownCancellationToken.IsCancellationRequested)
            {
                // Ctrl+C forced an exit
                return 0;
            }
            catch (Exception ex)
            {
                reporter.Error(ex.ToString());
                reporter.Error("An unexpected error occurred");
                return 1;
            }
            finally
            {
                console.CancelKeyPress -= OnCancelKeyPress;
                shutdownCancellationSource.Dispose();
            }

            void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
            {
                // if we already canceled, we force immediate shutdown:
                var forceShutdown = shutdownCancellationSource.IsCancellationRequested;

                if (!forceShutdown)
                {
                    reporter.Report(MessageDescriptor.ShutdownRequested);
                    shutdownCancellationSource.Cancel();
                    args.Cancel = true;
                }
                else
                {
                    Environment.Exit(0);
                }
            }
        }

        // internal for testing
        internal Watcher CreateWatcher(IRuntimeProcessLauncherFactory? runtimeProcessLauncherFactory)
        {
            if (environmentOptions.IsPollingEnabled)
            {
                reporter.Output("Polling file watcher is enabled");
            }

            var fileSetFactory = new MSBuildFileSetFactory(
                rootProjectOptions.ProjectPath,
                rootProjectOptions.TargetFramework,
                rootProjectOptions.BuildProperties,
                environmentOptions,
                reporter);

            bool enableHotReload;
            if (rootProjectOptions.Command != "run")
            {
                reporter.Verbose($"Command '{rootProjectOptions.Command}' does not support Hot Reload.");
                enableHotReload = false;
            }
            else if (options.GlobalOptions.NoHotReload)
            {
                reporter.Verbose("Hot Reload disabled by command line switch.");
                enableHotReload = false;
            }
            else
            {
                reporter.Report(MessageDescriptor.WatchingWithHotReload);
                enableHotReload = true;
            }

            var context = new DotNetWatchContext
            {
                Reporter = reporter,
                Options = options.GlobalOptions,
                EnvironmentOptions = environmentOptions,
                RootProjectOptions = rootProjectOptions,
            };

            return enableHotReload
                ? new HotReloadDotNetWatcher(context, console, fileSetFactory, runtimeProcessLauncherFactory)
                : new DotNetWatcher(context, fileSetFactory);
        }

        private async Task<int> ListFilesAsync(CancellationToken cancellationToken)
        {
            var fileSetFactory = new MSBuildFileSetFactory(
                rootProjectOptions.ProjectPath,
                rootProjectOptions.TargetFramework,
                rootProjectOptions.BuildProperties,
                environmentOptions,
                reporter);

            if (await fileSetFactory.TryCreateAsync(requireProjectGraph: null, cancellationToken) is not { } evaluationResult)
            {
                return 1;
            }

            foreach (var (filePath, _) in evaluationResult.Files.OrderBy(e => e.Key))
            {
                console.Out.WriteLine(filePath);
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
