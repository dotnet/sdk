﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.Loader;
using Microsoft.Build.Locator;

namespace Microsoft.DotNet.Watch
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

                var environmentOptions = EnvironmentOptions.FromEnvironment();

                var program = TryCreate(
                    args,
                    new PhysicalConsole(environmentOptions.TestFlags),
                    environmentOptions,
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

            if (environmentOptions.TestFlags != TestFlags.None)
            {
                reporter.Verbose($"Test flags: {environmentOptions.TestFlags}");
            }

            if (!TryFindProject(workingDirectory, options, reporter, out var projectPath))
            {
                errorCode = 1;
                return null;
            }

            var rootProjectOptions = options.GetProjectOptions(projectPath, workingDirectory);
            errorCode = 0;
            return new Program(console, reporter, rootProjectOptions, options, environmentOptions);
        }

        /// <summary>
        /// Finds a compatible MSBuild project.
        /// <param name="searchBase">The base directory to search</param>
        /// <param name="project">The filename of the project. Can be null.</param>
        /// </summary>
        private static bool TryFindProject(string searchBase, CommandLineOptions options, IReporter reporter, [NotNullWhen(true)] out string? projectPath)
        {
            projectPath = options.ProjectPath ?? searchBase;

            if (!Path.IsPathRooted(projectPath))
            {
                projectPath = Path.Combine(searchBase, projectPath);
            }

            if (Directory.Exists(projectPath))
            {
                var projects = Directory.EnumerateFileSystemEntries(projectPath, "*.*proj", SearchOption.TopDirectoryOnly)
                    .Where(f => !".xproj".Equals(Path.GetExtension(f), StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (projects.Count > 1)
                {
                    reporter.Error(string.Format(CultureInfo.CurrentCulture, Resources.Error_MultipleProjectsFound, projectPath));
                    return false;
                }

                if (projects.Count == 0)
                {
                    reporter.Error(string.Format(CultureInfo.CurrentCulture, Resources.Error_NoProjectsFound, projectPath));
                    return false;
                }

                projectPath = projects[0];
                return true;
            }

            if (!File.Exists(projectPath))
            {
                reporter.Error(string.Format(CultureInfo.CurrentCulture, Resources.Error_ProjectPath_NotFound, projectPath));
                    return false;
            }

            return true;
        }

        // internal for testing
        internal async Task<int> RunAsync()
        {
            var shutdownCancellationSourceDisposed = false;
            var shutdownCancellationSource = new CancellationTokenSource();
            var shutdownCancellationToken = shutdownCancellationSource.Token;
            var processRunner = new ProcessRunner(environmentOptions.ProcessCleanupTimeout);

            console.KeyPressed += key =>
            {
                if (!shutdownCancellationSourceDisposed && key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.C)
                {
                    // if we already canceled, we force immediate shutdown:
                    var forceShutdown = shutdownCancellationSource.IsCancellationRequested;

                    if (!forceShutdown)
                    {
                        reporter.Report(MessageDescriptor.ShutdownRequested);
                        shutdownCancellationSource.Cancel();
                    }
                    else
                    {
                        Environment.Exit(0);
                    }
                }
            };

            try
            {
                if (shutdownCancellationToken.IsCancellationRequested)
                {
                    return 1;
                }

                if (options.List)
                {
                    return await ListFilesAsync(processRunner, shutdownCancellationToken);
                }

                if (environmentOptions.IsPollingEnabled)
                {
                    reporter.Output("Polling file watcher is enabled");
                }

                var context = CreateContext(processRunner);

                if (IsHotReloadEnabled())
                {
                    var watcher = new HotReloadDotNetWatcher(context, console, runtimeProcessLauncherFactory: null);
                    await watcher.WatchAsync(shutdownCancellationToken);
                }
                else
                {
                    await DotNetWatcher.WatchAsync(context, shutdownCancellationToken);
                }

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
                shutdownCancellationSourceDisposed = true;
                shutdownCancellationSource.Dispose();
            }
        }

        // internal for testing
        internal DotNetWatchContext CreateContext(ProcessRunner processRunner)
            => new()
            {
                Reporter = reporter,
                ProcessRunner = processRunner,
                Options = options.GlobalOptions,
                EnvironmentOptions = environmentOptions,
                RootProjectOptions = rootProjectOptions,
            };

        private bool IsHotReloadEnabled()
        {
            if (rootProjectOptions.Command != "run")
            {
                reporter.Verbose($"Command '{rootProjectOptions.Command}' does not support Hot Reload.");
                return false;
            }

            if (options.GlobalOptions.NoHotReload)
            {
                reporter.Verbose("Hot Reload disabled by command line switch.");
                return false;
            }

            reporter.Report(MessageDescriptor.WatchingWithHotReload);
            return true;
        }

        private async Task<int> ListFilesAsync(ProcessRunner processRunner, CancellationToken cancellationToken)
        {
            var fileSetFactory = new MSBuildFileSetFactory(
                rootProjectOptions.ProjectPath,
                rootProjectOptions.BuildArguments,
                processRunner,
                new BuildReporter(reporter, environmentOptions));

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
