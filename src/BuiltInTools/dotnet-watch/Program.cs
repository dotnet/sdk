// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Loader;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Graph;
using Microsoft.Build.Locator;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;
using IConsole = Microsoft.Extensions.Tools.Internal.IConsole;
using Resources = Microsoft.DotNet.Watcher.Tools.Resources;
using System.Diagnostics;

namespace Microsoft.DotNet.Watcher
{
    internal sealed class Program : IDisposable
    {
        private const string Description = @"
Environment variables:

  DOTNET_USE_POLLING_FILE_WATCHER
  When set to '1' or 'true', dotnet-watch will poll the file system for
  changes. This is required for some file systems, such as network shares,
  Docker mounted volumes, and other virtual file systems.

  DOTNET_WATCH
  dotnet-watch sets this variable to '1' on all child processes launched.

  DOTNET_WATCH_ITERATION
  dotnet-watch sets this variable to '1' and increments by one each time
  a file is changed and the command is restarted.

  DOTNET_WATCH_SUPPRESS_EMOJIS
  When set to '1' or 'true', dotnet-watch will not show emojis in the 
  console output.

Remarks:
  The special option '--' is used to delimit the end of the options and
  the beginning of arguments that will be passed to the child dotnet process.
  Its use is optional. When the special option '--' is not used,
  dotnet-watch will use the first unrecognized argument as the beginning
  of all arguments passed into the child dotnet process.

  For example: dotnet watch -- --verbose run

  Even though '--verbose' is an option dotnet-watch supports, the use of '--'
  indicates that '--verbose' should be treated instead as an argument for
  dotnet-run.

Examples:
  dotnet watch run
  dotnet watch test
";
        private readonly IConsole _console;
        private readonly string _workingDirectory;
        private readonly string _muxerPath;
        private readonly CancellationTokenSource _cts;
        private IReporter _reporter;
        private IRequester _requester;

        public Program(IConsole console, string workingDirectory, string muxerPath)
        {
            Ensure.NotNull(console, nameof(console));
            Ensure.NotNullOrEmpty(workingDirectory, nameof(workingDirectory));

            _console = console;
            _workingDirectory = workingDirectory;
            _muxerPath = muxerPath;
            _cts = new CancellationTokenSource();
            console.CancelKeyPress += OnCancelKeyPress;

            var suppressEmojis = ShouldSuppressEmojis();
            _reporter = CreateReporter(verbose: true, quiet: false, console: _console, suppressEmojis);
            _requester = new ConsoleRequester(_console, quiet: false, suppressEmojis);
        }

        public static async Task<int> Main(string[] args)
        {
            try
            {
                var muxerPath = Environment.ProcessPath;
                Debug.Assert(Path.GetFileNameWithoutExtension(muxerPath) == "dotnet", $"Invalid muxer path {muxerPath}");

#if DEBUG
                var sdkRootDirectory = Environment.GetEnvironmentVariable("DOTNET_WATCH_DEBUG_SDK_DIRECTORY");
#else
                var sdkRootDirectory = "";
#endif

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

                using var program = new Program(PhysicalConsole.Singleton, Directory.GetCurrentDirectory(), muxerPath);
                return await program.RunAsync(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Unexpected error:");
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        internal Task<int> RunAsync(string[] args)
        {
            CliCommand rootCommand = CreateRootCommand(HandleWatch, _reporter);
            return rootCommand.Parse(args).InvokeAsync(_cts.Token);
        }

        internal static CliRootCommand CreateRootCommand(Func<CommandLineOptions, Task<int>> handler, IReporter reporter)
        {
            CliOption<bool> quiet = new("--quiet", "-q")
            {
                Description = "Suppresses all output except warnings and errors"
            };

            CliOption<bool> verbose = new("--verbose", "-v")
            {
                Description = "Show verbose output"
            };

            verbose.Validators.Add(v =>
            {
                if (v.FindResultFor(quiet) is not null && v.FindResultFor(verbose) is not null)
                {
                    v.AddError(Resources.Error_QuietAndVerboseSpecified);
                }
            });

            CliOption<bool> listOption = new("--list") { Description = "Lists all discovered files without starting the watcher." };
            CliOption<string> shortProjectOption = new("-p") { Description = "The project to watch.", Hidden = true };
            CliOption<string> longProjectOption = new("--project") { Description = "The project to watch" };
            CliOption<string> launchProfileOption = new("--launch-profile", "-lp")
            {
                Description = "The launch profile to start the project with (case-sensitive). " +
                "This option is only supported when running 'dotnet watch' or 'dotnet watch run'."
            };
            CliOption<bool> noHotReloadOption = new("--no-hot-reload") { Description = "Suppress hot reload for supported apps." };
            CliOption<bool> nonInteractiveOption = new("--non-interactive")
            {
                Description = "Runs dotnet-watch in non-interactive mode. This option is only supported when running with Hot Reload enabled. " +
                "Use this option to prevent console input from being captured."
            };
            CliArgument<string[]> forwardedArguments = new("forwardedArgs") { Description = "Arguments to pass to the child dotnet process." };

            CliRootCommand root = new (Description)
            {
                 quiet,
                 verbose,
                 noHotReloadOption,
                 nonInteractiveOption,
                 longProjectOption,
                 shortProjectOption,
                 launchProfileOption,
                 listOption,
                 forwardedArguments
            };

            root.SetAction((ParseResult parseResult, CancellationToken cancellationToken) =>
            {
                string projectValue = parseResult.GetValue(longProjectOption);
                if (string.IsNullOrEmpty(projectValue))
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    var projectShortValue = parseResult.GetValue(shortProjectOption);
#pragma warning restore CS0618 // Type or member is obsolete
                    if (!string.IsNullOrEmpty(projectShortValue))
                    {
                        reporter.Warn(Resources.Warning_ProjectAbbreviationDeprecated);
                        projectValue = projectShortValue;
                    }
                }

                CommandLineOptions options = new()
                {
                    Quiet = parseResult.GetValue(quiet),
                    List = parseResult.GetValue(listOption),
                    NoHotReload = parseResult.GetValue(noHotReloadOption),
                    NonInteractive = parseResult.GetValue(nonInteractiveOption),
                    Verbose = parseResult.GetValue(verbose),
                    Project = projectValue,
                    LaunchProfile = parseResult.GetValue(launchProfileOption),
                    RemainingArguments = parseResult.GetValue(forwardedArguments),
                };

                return handler(options);

            });
            return root;
        }

        private async Task<int> HandleWatch(CommandLineOptions options)
        {
            // update reporter as configured by options
            var suppressEmojis = ShouldSuppressEmojis();
            _reporter = CreateReporter(options.Verbose, options.Quiet, _console, suppressEmojis);
            _requester = new ConsoleRequester(_console, quiet: options.Quiet, suppressEmojis);

            try
            {
                if (_cts.IsCancellationRequested)
                {
                    return 1;
                }

                if (options.List)
                {
                    return await ListFilesAsync(_reporter,
                        options.Project,
                        _cts.Token);
                }
                else
                {
                    return await MainInternalAsync(options, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is OperationCanceledException)
                {
                    // swallow when only exception is the CTRL+C forced an exit
                    return 0;
                }

                _reporter.Error(ex.ToString());
                _reporter.Error("An unexpected error occurred");
                return 1;
            }
        }

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
        {
            // suppress CTRL+C on the first press
            args.Cancel = !_cts.IsCancellationRequested;

            if (args.Cancel)
            {
                _reporter.Output("Shutdown requested. Press Ctrl+C again to force exit.", emoji: "🛑");
            }

            _cts.Cancel();
        }

        private async Task<int> MainInternalAsync(CommandLineOptions options, CancellationToken cancellationToken)
        {
            // TODO multiple projects should be easy enough to add here
            string projectFile;
            try
            {
                projectFile = MsBuildProjectFinder.FindMsBuildProject(_workingDirectory, options.Project);
            }
            catch (FileNotFoundException ex)
            {
                _reporter.Error(ex.Message);
                return 1;
            }

            var args = options.RemainingArguments;

            var isDefaultRunCommand = false;
            if (args.Count == 1 && args[0] == "run")
            {
                isDefaultRunCommand = true;
            }
            else if (args.Count == 0)
            {
                isDefaultRunCommand = true;
                args = new[] { "run" };
            }

            var watchOptions = DotNetWatchOptions.Default;
            watchOptions.NonInteractive = options.NonInteractive;

            var fileSetFactory = new MsBuildFileSetFactory(
                _reporter,
                watchOptions,
                _muxerPath,
                projectFile,
                waitOnError: true,
                trace: false);

            var processInfo = new ProcessSpec
            {
                Executable = _muxerPath,
                WorkingDirectory = Path.GetDirectoryName(projectFile),
                Arguments = args,
                EnvironmentVariables =
                {
                    ["DOTNET_WATCH"] = "1"
                },
            };

            if (CommandLineOptions.IsPollingEnabled)
            {
                _reporter.Output("Polling file watcher is enabled");
            }

            var launchProfile = LaunchSettingsProfile.ReadLaunchProfile(processInfo.WorkingDirectory, options.LaunchProfile, _reporter) ?? new();

            var context = new DotNetWatchContext
            {
                ProcessSpec = processInfo,
                Reporter = _reporter,
                SuppressMSBuildIncrementalism = watchOptions.SuppressMSBuildIncrementalism,
                LaunchSettingsProfile = launchProfile,
            };

            context.ProjectGraph = TryReadProject(projectFile);

            if (!options.NoHotReload && isDefaultRunCommand && context.ProjectGraph is not null && IsHotReloadSupported(context.ProjectGraph))
            {
                _reporter.Verbose($"Project supports hot reload and was configured to run with the default run-command. Watching with hot-reload");

                // Use hot-reload based watching if
                // a) watch was invoked with no args or with exactly one arg - the run command e.g. `dotnet watch` or `dotnet watch run`
                // b) The launch profile supports hot-reload based watching.
                // The watcher will complain if users configure this for runtimes that would not support it.
                await using var watcher = new HotReloadDotNetWatcher(_reporter, _requester, fileSetFactory, watchOptions, _console, _workingDirectory, _muxerPath);
                await watcher.WatchAsync(context, cancellationToken);
            }
            else
            {
                _reporter.Verbose("Did not find a HotReloadProfile or running a non-default command. Watching with legacy behavior.");

                // We'll use the presence of a profile to decide if we're going to use the hot-reload based watching.
                // The watcher will complain if users configure this for runtimes that would not support it.
                await using var watcher = new DotNetWatcher(_reporter, fileSetFactory, watchOptions, _muxerPath);
                await watcher.WatchAsync(context, cancellationToken);
            }

            return 0;
        }

        private ProjectGraph TryReadProject(string project)
        {
            try
            {
                return new ProjectGraph(project);
            }
            catch (Exception ex)
            {
                _reporter.Verbose("Reading the project instance failed.");
                _reporter.Verbose(ex.ToString());
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

        private async Task<int> ListFilesAsync(
            IReporter reporter,
            string project,
            CancellationToken cancellationToken)
        {
            // TODO multiple projects should be easy enough to add here
            string projectFile;
            try
            {
                projectFile = MsBuildProjectFinder.FindMsBuildProject(_workingDirectory, project);
            }
            catch (FileNotFoundException ex)
            {
                reporter.Error(ex.Message);
                return 1;
            }

            var fileSetFactory = new MsBuildFileSetFactory(
                reporter,
                DotNetWatchOptions.Default,
                _muxerPath,
                projectFile,
                waitOnError: false,
                trace: false);

            var files = await fileSetFactory.CreateAsync(cancellationToken);

            if (files == null)
            {
                return 1;
            }

            foreach (var file in files)
            {
                _console.Out.WriteLine(file.FilePath);
            }

            return 0;
        }

        private static IReporter CreateReporter(bool verbose, bool quiet, IConsole console, bool suppressEmojis)
            => new ConsoleReporter(console, verbose || IsGlobalVerbose(), quiet, suppressEmojis);

        private static bool IsGlobalVerbose()
        {
            bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE"), out bool globalVerbose);
            return globalVerbose;
        }

        public void Dispose()
        {
            _console.CancelKeyPress -= OnCancelKeyPress;
            _cts.Dispose();
        }

        private static bool ShouldSuppressEmojis()
        {
            var suppressEmojisEnvironmentVariable = Environment.GetEnvironmentVariable("DOTNET_WATCH_SUPPRESS_EMOJIS");
            var suppressEmojis = suppressEmojisEnvironmentVariable == "1" || string.Equals(suppressEmojisEnvironmentVariable, "true", StringComparison.OrdinalIgnoreCase);
            return suppressEmojis;
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
