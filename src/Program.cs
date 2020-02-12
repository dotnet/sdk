// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Tools.Logging;
using Microsoft.CodeAnalysis.Tools.MSBuild;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal class Program
    {
        private static readonly string[] _verbosityLevels = new[] { "q", "quiet", "m", "minimal", "n", "normal", "d", "detailed", "diag", "diagnostic" };

        private static async Task<int> Main(string[] args)
        {
            var parser = new CommandLineBuilder(new Command("dotnet-format", handler: CommandHandler.Create(typeof(Program).GetMethod(nameof(Run)))))
                .UseParseDirective()
                .UseHelp()
                .UseDebugDirective()
                .UseSuggestDirective()
                .RegisterWithDotnetSuggest()
                .UseParseErrorReporting()
                .UseExceptionHandler()
                .AddOption(new Option(new[] { "-f", "--folder" }, Resources.The_folder_to_operate_on_Cannot_be_used_with_the_workspace_option, new Argument<string>(() => null)))
                .AddOption(new Option(new[] { "-w", "--workspace" }, Resources.The_solution_or_project_file_to_operate_on_If_a_file_is_not_specified_the_command_will_search_the_current_directory_for_one, new Argument<string>(() => null)))
                .AddOption(new Option(new[] { "-v", "--verbosity" }, Resources.Set_the_verbosity_level_Allowed_values_are_quiet_minimal_normal_detailed_and_diagnostic, new Argument<string>() { Arity = ArgumentArity.ExactlyOne }.FromAmong(_verbosityLevels)))
                .AddOption(new Option(new[] { "--dry-run" }, Resources.Format_files_but_do_not_save_changes_to_disk, new Argument<bool>()))
                .AddOption(new Option(new[] { "--check" }, Resources.Terminate_with_a_non_zero_exit_code_if_any_files_were_formatted, new Argument<bool>()))
                .AddOption(new Option(new[] { "--files" }, Resources.A_comma_separated_list_of_relative_file_paths_to_format_All_files_are_formatted_if_empty, new Argument<string>(() => null)))
                .AddOption(new Option(new[] { "--exclude" }, Resources.A_comma_separated_list_of_relative_file_or_folder_paths_to_exclude_from_formatting, new Argument<string>(() => null)))
                .AddOption(new Option(new[] { "--report" }, Resources.Accepts_a_file_path_which_if_provided_will_produce_a_format_report_json_file_in_the_given_directory, new Argument<string>(() => null)))
                .UseVersionOption()
                .Build();

            return await parser.InvokeAsync(args).ConfigureAwait(false);
        }

        public static async Task<int> Run(string folder, string workspace, string verbosity, bool dryRun, bool check, string files, string exclude, string report, IConsole console = null)
        {
            // Setup logging.
            var serviceCollection = new ServiceCollection();
            var logLevel = GetLogLevel(verbosity);
            ConfigureServices(serviceCollection, console, logLevel);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var logger = serviceProvider.GetService<ILogger<Program>>();

            // Hook so we can cancel and exit when ctrl+c is pressed.
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            var currentDirectory = string.Empty;

            try
            {
                currentDirectory = Environment.CurrentDirectory;

                string workspaceDirectory;
                string workspacePath;
                WorkspaceType workspaceType;

                if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(workspace))
                {
                    logger.LogWarning(Resources.Cannot_specify_both_folder_and_workspace_options);
                    return 1;
                }

                if (!string.IsNullOrEmpty(folder))
                {
                    folder = Path.GetFullPath(folder, Environment.CurrentDirectory);
                    workspacePath = folder;
                    workspaceDirectory = workspacePath;
                    workspaceType = WorkspaceType.Folder;
                }
                else
                {
                    var (isSolution, workspaceFilePath) = MSBuildWorkspaceFinder.FindWorkspace(currentDirectory, workspace);

                    workspacePath = workspaceFilePath;
                    workspaceType = isSolution
                        ? WorkspaceType.Solution
                        : WorkspaceType.Project;

                    // To ensure we get the version of MSBuild packaged with the dotnet SDK used by the
                    // workspace, use its directory as our working directory which will take into account
                    // a global.json if present.
                    workspaceDirectory = Path.GetDirectoryName(workspacePath);
                }

                Environment.CurrentDirectory = workspaceDirectory;

                var filesToFormat = GetFiles(files, folder);
                var filesToIgnore = GetFiles(exclude, folder);

                // Since we are running as a dotnet tool we should be able to find an instance of
                // MSBuild in a .NET Core SDK.
                var msBuildInstance = Build.Locator.MSBuildLocator.QueryVisualStudioInstances().First();

                // Since we do not inherit msbuild.deps.json when referencing the SDK copy
                // of MSBuild and because the SDK no longer ships with version matched assemblies, we
                // register an assembly loader that will load assemblies from the msbuild path with
                // equal or higher version numbers than requested.
                LooseVersionAssemblyLoader.Register(msBuildInstance.MSBuildPath);

                Build.Locator.MSBuildLocator.RegisterInstance(msBuildInstance);

                var formatOptions = new FormatOptions(
                    workspacePath,
                    workspaceType,
                    logLevel,
                    saveFormattedFiles: !dryRun,
                    changesAreErrors: check,
                    filesToFormat,
                    filesToIgnore,
                    reportPath: report);

                var formatResult = await CodeFormatter.FormatWorkspaceAsync(
                    formatOptions,
                    logger,
                    cancellationTokenSource.Token).ConfigureAwait(false);

                return GetExitCode(formatResult, check);
            }
            catch (FileNotFoundException fex)
            {
                logger.LogError(fex.Message);
                return 1;
            }
            catch (OperationCanceledException)
            {
                return 1;
            }
            finally
            {
                if (!string.IsNullOrEmpty(currentDirectory))
                {
                    Environment.CurrentDirectory = currentDirectory;
                }
            }
        }

        internal static int GetExitCode(WorkspaceFormatResult formatResult, bool check)
        {
            if (!check)
            {
                return formatResult.ExitCode;
            }

            return formatResult.FilesFormatted == 0 ? 0 : 1;
        }

        internal static LogLevel GetLogLevel(string verbosity)
        {
            switch (verbosity)
            {
                case "q":
                case "quiet":
                    return LogLevel.Error;
                case "m":
                case "minimal":
                    return LogLevel.Warning;
                case "n":
                case "normal":
                    return LogLevel.Information;
                case "d":
                case "detailed":
                    return LogLevel.Debug;
                case "diag":
                case "diagnostic":
                    return LogLevel.Trace;
                default:
                    return LogLevel.Information;
            }
        }

        private static void ConfigureServices(ServiceCollection serviceCollection, IConsole console, LogLevel logLevel)
        {
            serviceCollection.AddSingleton(new LoggerFactory().AddSimpleConsole(console, logLevel));
            serviceCollection.AddLogging();
        }

        /// <summary>
        /// Converts a comma-separated list of relative file paths to a hashmap of full file paths.
        /// </summary>
        internal static ImmutableHashSet<string> GetFiles(string files, string folder)
        {
            if (string.IsNullOrEmpty(files))
            {
                return ImmutableHashSet.Create<string>();
            }

            if (string.IsNullOrEmpty(folder))
            {
                return files.Split(',')
                    .Select(path => Path.GetFullPath(path, Environment.CurrentDirectory))
                    .ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                return files.Split(',')
                    .Select(path => Path.GetFullPath(path, Environment.CurrentDirectory))
                    .Where(path => path.StartsWith(folder))
                    .ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
