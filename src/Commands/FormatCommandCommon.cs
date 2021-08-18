// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.Tools.Logging;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.CodeAnalysis.Tools.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal static class FormatCommandCommon
    {
        internal const int UnhandledExceptionExitCode = 1;
        internal const int CheckFailedExitCode = 2;
        internal const int UnableToLocateMSBuildExitCode = 3;
        internal const int UnableToLocateDotNetCliExitCode = 4;

        private static string[] VerbosityLevels => new[] { "q", "quiet", "m", "minimal", "n", "normal", "d", "detailed", "diag", "diagnostic" };
        private static string[] SeverityLevels => new[] { "info", "warn", "error" };

        public static readonly Argument<string> SlnOrProjectArgument = new Argument<string>(Resources.SolutionOrProjectArgumentName)
        {
            Description = Resources.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrOne
        }.DefaultToCurrentDirectory();

        internal static readonly Option<bool> FolderOption = new(new[] { "--folder" }, Resources.Whether_to_treat_the_workspace_argument_as_a_simple_folder_of_files);
        internal static readonly Option<bool> NoRestoreOption = new(new[] { "--no-restore" }, Resources.Doesnt_execute_an_implicit_restore_before_formatting);
        internal static readonly Option<bool> VerifyNoChanges = new(new[] { "--verify-no-changes" }, Resources.Verify_no_formatting_changes_would_be_performed_Terminates_with_a_non_zero_exit_code_if_any_files_would_have_been_formatted);
        internal static readonly Option<string[]> DiagnosticsOption = new(new[] { "--diagnostics" }, () => Array.Empty<string>(), Resources.A_space_separated_list_of_diagnostic_ids_to_use_as_a_filter_when_fixing_code_style_or_3rd_party_issues)
        {
            AllowMultipleArgumentsPerToken = true
        };
        internal static readonly Option<string> SeverityOption = new Option<string>("--severity", Resources.The_severity_of_diagnostics_to_fix_Allowed_values_are_info_warn_and_error).FromAmong(SeverityLevels);
        internal static readonly Option<string[]> IncludeOption = new(new[] { "--include" }, () => Array.Empty<string>(), Resources.A_list_of_relative_file_or_folder_paths_to_include_in_formatting_All_files_are_formatted_if_empty)
        {
            AllowMultipleArgumentsPerToken = true
        };
        internal static readonly Option<string[]> ExcludeOption = new(new[] { "--exclude" }, () => Array.Empty<string>(), Resources.A_list_of_relative_file_or_folder_paths_to_exclude_from_formatting)
        {
            AllowMultipleArgumentsPerToken = true
        };
        internal static readonly Option<bool> IncludeGeneratedOption = new(new[] { "--include-generated" }, Resources.Format_files_generated_by_the_SDK);
        internal static readonly Option<string> VerbosityOption = new Option<string>(new[] { "--verbosity", "-v" }, Resources.Set_the_verbosity_level_Allowed_values_are_quiet_minimal_normal_detailed_and_diagnostic).FromAmong(VerbosityLevels);
        internal static readonly Option BinarylogOption = new Option(new[] { "--binarylog" }, Resources.Log_all_project_or_solution_load_information_to_a_binary_log_file, argumentType: typeof(string), arity: ArgumentArity.ZeroOrOne)
        {
            ArgumentHelpName = "binary-log-path"
        }.LegalFilePathsOnly();
        internal static readonly Option ReportOption = new Option(new[] { "--report" }, Resources.Accepts_a_file_path_which_if_provided_will_produce_a_json_report_in_the_given_directory, argumentType: typeof(string), arity: ArgumentArity.ZeroOrOne)
        {
            ArgumentHelpName = "report-path"
        }.LegalFilePathsOnly();

        public static void AddCommonOptions(this Command command)
        {
            command.AddArgument(SlnOrProjectArgument);
            command.AddOption(NoRestoreOption);
            command.AddOption(VerifyNoChanges);
            command.AddOption(IncludeOption);
            command.AddOption(ExcludeOption);
            command.AddOption(IncludeGeneratedOption);
            command.AddOption(VerbosityOption);
            command.AddOption(BinarylogOption);
            command.AddOption(ReportOption);
        }

        public static Argument<T> DefaultToCurrentDirectory<T>(this Argument<T> arg)
        {
            arg.SetDefaultValue(EnsureTrailingSlash(Directory.GetCurrentDirectory()));
            return arg;
        }

        public static ILogger<Program> SetupLogging(this IConsole console, LogLevel minimalLogLevel, LogLevel minimalErrorLevel)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(new LoggerFactory().AddSimpleConsole(console, minimalLogLevel, minimalErrorLevel));
            serviceCollection.AddLogging();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var logger = serviceProvider.GetService<ILogger<Program>>();

            return logger!;
        }

        public static int GetExitCode(this WorkspaceFormatResult formatResult, bool check)
        {
            if (!check)
            {
                return formatResult.ExitCode;
            }

            return formatResult.FilesFormatted == 0 ? 0 : CheckFailedExitCode;
        }

        public static FormatOptions ParseVerbosityOption(this ParseResult parseResult, FormatOptions formatOptions)
        {
            if (parseResult.HasOption(VerbosityOption) &&
                parseResult.ValueForOption(VerbosityOption) is string { Length: > 0 } verbosity)
            {
                formatOptions = formatOptions with { LogLevel = GetLogLevel(verbosity) };
            }
            else
            {
                formatOptions = formatOptions with { LogLevel = LogLevel.Warning };
            }

            return formatOptions;
        }

        public static FormatOptions ParseCommonOptions(this ParseResult parseResult, FormatOptions formatOptions, ILogger logger)
        {
            if (parseResult.HasOption(NoRestoreOption))
            {
                formatOptions = formatOptions with { NoRestore = true };
            }

            if (parseResult.HasOption(VerifyNoChanges))
            {
                formatOptions = formatOptions with { ChangesAreErrors = true };
                formatOptions = formatOptions with { SaveFormattedFiles = false };
            }

            if (parseResult.HasOption(IncludeGeneratedOption))
            {
                formatOptions = formatOptions with { IncludeGeneratedFiles = true };
            }

            if (parseResult.HasOption(IncludeOption) || parseResult.HasOption(ExcludeOption))
            {
                var fileToInclude = parseResult.ValueForOption(IncludeOption) ?? Array.Empty<string>();
                var fileToExclude = parseResult.ValueForOption(ExcludeOption) ?? Array.Empty<string>();
                HandleStandardInput(logger, ref fileToInclude, ref fileToExclude);
                formatOptions = formatOptions with { FileMatcher = SourceFileMatcher.CreateMatcher(fileToInclude, fileToExclude) };
            }

            if (parseResult.HasOption(ReportOption))
            {
                formatOptions = formatOptions with { ReportPath = string.Empty };

                if (parseResult.ValueForOption(ReportOption) is string { Length: > 0 } reportPath)
                {
                    formatOptions = formatOptions with { ReportPath = reportPath };
                }
            }

            if (parseResult.HasOption(BinarylogOption))
            {
                formatOptions = formatOptions with { BinaryLogPath = "format.binlog" };

                if (parseResult.ValueForOption(BinarylogOption) is string { Length: > 0 } binaryLogPath)
                {
                    formatOptions = Path.GetExtension(binaryLogPath)?.Equals(".binlog") == false
                        ? (formatOptions with { BinaryLogPath = Path.ChangeExtension(binaryLogPath, ".binlog") })
                        : (formatOptions with { BinaryLogPath = binaryLogPath });
                }
            }

            return formatOptions;

            static void HandleStandardInput(ILogger logger, ref string[] include, ref string[] exclude)
            {
                string[] s_standardInputKeywords = { "/dev/stdin", "-" };
                const int CheckFailedExitCode = 2;

                var isStandardMarkerUsed = false;
                if (include.Length == 1 && s_standardInputKeywords.Contains(include[0]))
                {
                    if (TryReadFromStandardInput(ref include))
                    {
                        isStandardMarkerUsed = true;
                    }
                }

                if (exclude.Length == 1 && s_standardInputKeywords.Contains(exclude[0]))
                {
                    if (isStandardMarkerUsed)
                    {
                        logger.LogCritical(Resources.Standard_input_used_multiple_times);
                        Environment.Exit(CheckFailedExitCode);
                    }

                    TryReadFromStandardInput(ref exclude);
                }

                static bool TryReadFromStandardInput(ref string[] subject)
                {
                    if (!Console.IsInputRedirected)
                    {
                        return false; // pass
                    }

                    // reset the subject array
                    Array.Clear(subject, 0, subject.Length);
                    Array.Resize(ref subject, 0);

                    Console.InputEncoding = Encoding.UTF8;
                    using var reader = new StreamReader(Console.OpenStandardInput(8192));
                    Console.SetIn(reader);

                    for (var i = 0; Console.In.Peek() != -1; ++i)
                    {
                        var line = Console.In.ReadLine();
                        if (line is null)
                        {
                            continue;
                        }

                        Array.Resize(ref subject, subject.Length + 1);
                        subject[i] = line;
                    }

                    return true;
                }
            }
        }

        internal static LogLevel GetLogLevel(string? verbosity)
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

        public static FormatOptions ParseWorkspaceOptions(this ParseResult parseResult, FormatOptions formatOptions)
        {
            var currentDirectory = Environment.CurrentDirectory;

            if (parseResult.ValueForArgument<string>(SlnOrProjectArgument) is string { Length: > 0 } slnOrProject)
            {
                var (isSolution, workspaceFilePath) = MSBuildWorkspaceFinder.FindWorkspace(currentDirectory, slnOrProject);
                formatOptions = formatOptions with { WorkspaceFilePath = workspaceFilePath };
                formatOptions = formatOptions with { WorkspaceType = isSolution ? WorkspaceType.Solution : WorkspaceType.Project };

                // To ensure we get the version of MSBuild packaged with the dotnet SDK used by the
                // workspace, use its directory as our working directory which will take into account
                // a global.json if present.
                var workspaceDirectory = Path.GetDirectoryName(workspaceFilePath);
                if (workspaceDirectory is null)
                {
                    throw new Exception($"Unable to find folder at '{workspaceFilePath}'");
                }
            }

            return formatOptions;
        }

        private static string EnsureTrailingSlash(string path)
            => !string.IsNullOrEmpty(path) &&
               path[^1] != Path.DirectorySeparatorChar
                ? path + Path.DirectorySeparatorChar
                : path;
    }
}
