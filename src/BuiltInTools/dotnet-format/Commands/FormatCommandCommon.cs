// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.CodeAnalysis.Tools.Logging;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.CodeAnalysis.Tools.Workspaces;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal static class FormatCommandCommon
    {
        internal const int UnhandledExceptionExitCode = 1;
        internal const int CheckFailedExitCode = 2;
        internal const int UnableToLocateMSBuildExitCode = 3;

        private static string[] VerbosityLevels => new[] { "q", "quiet", "m", "minimal", "n", "normal", "d", "detailed", "diag", "diagnostic" };
        private static string[] SeverityLevels => new[] { "info", "warn", "error" };

        public static readonly Argument<string> SlnOrProjectArgument = new Argument<string>(Resources.SolutionOrProjectArgumentName)
        {
            Description = Resources.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrOne
        }.DefaultToCurrentDirectory();

        internal static readonly Option<bool> FolderOption = new("--folder")
        {
            Description = Resources.Whether_to_treat_the_workspace_argument_as_a_simple_folder_of_files,
        };
        internal static readonly Option<bool> NoRestoreOption = new("--no-restore")
        {
            Description = Resources.Doesnt_execute_an_implicit_restore_before_formatting,
        };
        internal static readonly Option<bool> VerifyNoChanges = new("--verify-no-changes")
        {
            Description = Resources.Verify_no_formatting_changes_would_be_performed_Terminates_with_a_non_zero_exit_code_if_any_files_would_have_been_formatted,
        };
        internal static readonly Option<string[]> DiagnosticsOption = new("--diagnostics")
        {
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = _ => Array.Empty<string>(),
            Description = Resources.A_space_separated_list_of_diagnostic_ids_to_use_as_a_filter_when_fixing_code_style_or_3rd_party_issues,
        };
        internal static readonly Option<string[]> ExcludeDiagnosticsOption = new("--exclude-diagnostics")
        {
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = _ => Array.Empty<string>(),
            Description = Resources.A_space_separated_list_of_diagnostic_ids_to_ignore_when_fixing_code_style_or_3rd_party_issues,
        };
        internal static readonly Option<string> SeverityOption = new Option<string>("--severity")
        {
            Description = Resources.The_severity_of_diagnostics_to_fix_Allowed_values_are_info_warn_and_error,
        };
        internal static readonly Option<string[]> IncludeOption = new("--include")
        {
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = _ => Array.Empty<string>(),
            Description = Resources.A_list_of_relative_file_or_folder_paths_to_include_in_formatting_All_files_are_formatted_if_empty,
        };
        internal static readonly Option<string[]> ExcludeOption = new("--exclude")
        {
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = _ => Array.Empty<string>(),
            Description = Resources.A_list_of_relative_file_or_folder_paths_to_exclude_from_formatting,
        };
        internal static readonly Option<bool> IncludeGeneratedOption = new("--include-generated")
        {
            Description = Resources.Format_files_generated_by_the_SDK,
        };
        internal static readonly Option<string> VerbosityOption = new Option<string>("--verbosity", "-v")
        {
            Description = Resources.Set_the_verbosity_level_Allowed_values_are_quiet_minimal_normal_detailed_and_diagnostic,
        };
        internal static readonly Option<string> BinarylogOption = new Option<string>("--binarylog")
        {
            HelpName = "binary-log-path",
            Arity = ArgumentArity.ZeroOrOne,
            Description = Resources.Log_all_project_or_solution_load_information_to_a_binary_log_file,
        };
        internal static readonly Option<string> ReportOption = new Option<string>("--report")
        {
            HelpName = "report-path",
            Arity = ArgumentArity.ZeroOrOne,
            Description = Resources.Accepts_a_file_path_which_if_provided_will_produce_a_json_report_in_the_given_directory,
        };

        static FormatCommandCommon()
        {
            SeverityOption.AcceptOnlyFromAmong(SeverityLevels);
            VerbosityOption.AcceptOnlyFromAmong(VerbosityLevels);
            BinarylogOption.AcceptLegalFilePathsOnly();
            ReportOption.AcceptLegalFilePathsOnly();
        }

        internal static async Task<int> FormatAsync(FormatOptions formatOptions, ILogger<Program> logger, CancellationToken cancellationToken)
        {
            if (formatOptions.WorkspaceType != WorkspaceType.Folder)
            {
                var runtimeVersion = GetRuntimeVersion();
                logger.LogDebug(Resources.The_dotnet_runtime_version_is_0, runtimeVersion);

                if (!TryLoadMSBuild(out var msBuildPath))
                {
                    logger.LogError(Resources.Unable_to_locate_MSBuild_Ensure_the_NET_SDK_was_installed_with_the_official_installer);
                    return UnableToLocateMSBuildExitCode;
                }

                logger.LogTrace(Resources.Using_msbuildexe_located_in_0, msBuildPath);
            }

            var formatResult = await CodeFormatter.FormatWorkspaceAsync(
                formatOptions,
                logger,
                cancellationToken,
                binaryLogPath: formatOptions.BinaryLogPath).ConfigureAwait(false);
            return formatResult.GetExitCode(formatOptions.ChangesAreErrors);
        }

        public static void AddCommonOptions(this Command command)
        {
            command.Arguments.Add(SlnOrProjectArgument);
            command.Options.Add(NoRestoreOption);
            command.Options.Add(VerifyNoChanges);
            command.Options.Add(IncludeOption);
            command.Options.Add(ExcludeOption);
            command.Options.Add(IncludeGeneratedOption);
            command.Options.Add(VerbosityOption);
            command.Options.Add(BinarylogOption);
            command.Options.Add(ReportOption);
        }

        public static Argument<string> DefaultToCurrentDirectory(this Argument<string> arg)
        {
            arg.DefaultValueFactory = _ => EnsureTrailingSlash(Directory.GetCurrentDirectory());
            return arg;
        }

        public static ILogger<Program> SetupLogging(this IConsole console, LogLevel minimalLogLevel, LogLevel minimalErrorLevel)
        {
            var loggerFactory = new LoggerFactory()
                .AddSimpleConsole(console, minimalLogLevel, minimalErrorLevel);
            var logger = loggerFactory.CreateLogger<Program>();
            return logger;
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
            if (parseResult.GetResult(VerbosityOption) is not null &&
                parseResult.GetValue(VerbosityOption) is string { Length: > 0 } verbosity)
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
            if (parseResult.GetResult(NoRestoreOption) is not null)
            {
                formatOptions = formatOptions with { NoRestore = true };
            }

            if (parseResult.GetResult(VerifyNoChanges) is not null)
            {
                formatOptions = formatOptions with { ChangesAreErrors = true };
                formatOptions = formatOptions with { SaveFormattedFiles = false };
            }

            if (parseResult.GetResult(IncludeGeneratedOption) is not null)
            {
                formatOptions = formatOptions with { IncludeGeneratedFiles = true };
            }

            if (parseResult.GetResult(IncludeOption) is not null || parseResult.GetResult(ExcludeOption) is not null)
            {
                var fileToInclude = parseResult.GetValue(IncludeOption) ?? Array.Empty<string>();
                var fileToExclude = parseResult.GetValue(ExcludeOption) ?? Array.Empty<string>();
                HandleStandardInput(logger, ref fileToInclude, ref fileToExclude);
                formatOptions = formatOptions with { FileMatcher = SourceFileMatcher.CreateMatcher(fileToInclude, fileToExclude) };
            }

            if (parseResult.GetResult(ReportOption) is not null)
            {
                formatOptions = formatOptions with { ReportPath = string.Empty };

                if (parseResult.GetValue(ReportOption) is string { Length: > 0 } reportPath)
                {
                    formatOptions = formatOptions with { ReportPath = reportPath };
                }
            }

            if (parseResult.GetResult(BinarylogOption) is not null)
            {
                formatOptions = formatOptions with { BinaryLogPath = "format.binlog" };

                if (parseResult.GetValue(BinarylogOption) is string { Length: > 0 } binaryLogPath)
                {
                    formatOptions = Path.GetExtension(binaryLogPath)?.Equals(".binlog") == false
                        ? (formatOptions with { BinaryLogPath = Path.ChangeExtension(binaryLogPath, ".binlog") })
                        : (formatOptions with { BinaryLogPath = binaryLogPath });
                }
            }

            return formatOptions;

            static void HandleStandardInput(ILogger logger, ref string[] include, ref string[] exclude)
            {
                string[] standardInputKeywords = { "/dev/stdin", "-" };
                const int CheckFailedExitCode = 2;

                var isStandardMarkerUsed = false;
                if (include.Length == 1 && standardInputKeywords.Contains(include[0]))
                {
                    if (TryReadFromStandardInput(ref include))
                    {
                        isStandardMarkerUsed = true;
                    }
                }

                if (exclude.Length == 1 && standardInputKeywords.Contains(exclude[0]))
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
            return verbosity switch
            {
                "q" or "quiet" => LogLevel.Error,
                "m" or "minimal" => LogLevel.Warning,
                "n" or "normal" => LogLevel.Information,
                "d" or "detailed" => LogLevel.Debug,
                "diag" or "diagnostic" => LogLevel.Trace,
                _ => LogLevel.Information,
            };
        }

        internal static DiagnosticSeverity GetSeverity(string? severity)
        {
            return severity?.ToLowerInvariant() switch
            {
                "" => DiagnosticSeverity.Error,
                FixSeverity.Error => DiagnosticSeverity.Error,
                FixSeverity.Warn => DiagnosticSeverity.Warning,
                FixSeverity.Info => DiagnosticSeverity.Info,
                _ => throw new ArgumentOutOfRangeException(nameof(severity)),
            };
        }

        public static FormatOptions ParseWorkspaceOptions(this ParseResult parseResult, FormatOptions formatOptions)
        {
            var currentDirectory = Environment.CurrentDirectory;

            if (parseResult.GetValue<string>(SlnOrProjectArgument) is string { Length: > 0 } slnOrProject)
            {
                if (parseResult.GetResult(FolderOption) is not null)
                {
                    formatOptions = formatOptions with { WorkspaceFilePath = slnOrProject };
                    formatOptions = formatOptions with { WorkspaceType = WorkspaceType.Folder };
                    return formatOptions;
                }

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

        internal static string? GetVersion()
        {
            return Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
        }

        internal static bool TryLoadMSBuild([NotNullWhen(returnValue: true)] out string? msBuildPath)
        {
            try
            {
                // Get the global.json pinned SDK or latest instance.
                var msBuildInstance = Build.Locator.MSBuildLocator.QueryVisualStudioInstances()
                    .Where(instance => instance.Version.Major >= 6)
                    .FirstOrDefault();
                if (msBuildInstance is null)
                {
                    msBuildPath = null;
                    return false;
                }

                msBuildPath = Path.EndsInDirectorySeparator(msBuildInstance.MSBuildPath)
                    ? msBuildInstance.MSBuildPath
                    : msBuildInstance.MSBuildPath + Path.DirectorySeparatorChar;

                Build.Locator.MSBuildLocator.RegisterMSBuildPath(msBuildPath);
                return true;
            }
            catch
            {
                msBuildPath = null;
                return false;
            }
        }

        internal static string GetRuntimeVersion()
        {
            var pathParts = typeof(string).Assembly.Location.Split('\\', '/');
            var netCoreAppIndex = Array.IndexOf(pathParts, "Microsoft.NETCore.App");
            return pathParts[netCoreAppIndex + 1];
        }
    }
}
