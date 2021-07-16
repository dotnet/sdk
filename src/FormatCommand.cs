// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Tools
{
    internal static class FormatCommand
    {
        // This delegate should be kept in Sync with the FormatCommand options and argument names
        // so that values bind correctly.
        internal delegate Task<int> Handler(
            string? workspace,
            bool noRestore,
            bool folder,
            bool fixWhitespace,
            string fixStyle,
            string fixAnalyzers,
            string[] diagnostics,
            string? verbosity,
            bool check,
            string[] include,
            string[] exclude,
            string? report,
            bool includeGenerated,
            string? binarylog,
            IConsole console);

        internal static string[] VerbosityLevels => new[] { "q", "quiet", "m", "minimal", "n", "normal", "d", "detailed", "diag", "diagnostic" };
        internal static string[] SeverityLevels => new[] { FixSeverity.Info, FixSeverity.Warn, FixSeverity.Error };

        internal static RootCommand CreateCommandLineOptions()
        {
            // Sync changes to option and argument names with the FormatCommant.Handler above.
            var rootCommand = new RootCommand
            {
                new Argument<string?>("workspace", () => null, Resources.A_path_to_a_solution_file_a_project_file_or_a_folder_containing_a_solution_or_project_file_If_a_path_is_not_specified_then_the_current_directory_is_used).LegalFilePathsOnly(),
                new Option<bool>(new[] { "--no-restore" }, Resources.Doesnt_execute_an_implicit_restore_before_formatting),
                new Option<bool>(new[] { "--folder", "-f" }, Resources.Whether_to_treat_the_workspace_argument_as_a_simple_folder_of_files),
                new Option<bool>(new[] { "--fix-whitespace", "-w" }, Resources.Run_whitespace_formatting_Run_by_default_when_not_applying_fixes),
                new Option(new[] { "--fix-style", "-s" }, Resources.Run_code_style_analyzers_and_apply_fixes, argumentType: typeof(string), arity: ArgumentArity.ZeroOrOne)
                {
                    ArgumentHelpName = "severity"
                }.FromAmong(SeverityLevels),
                new Option(new[] { "--fix-analyzers", "-a" }, Resources.Run_3rd_party_analyzers_and_apply_fixes, argumentType: typeof(string), arity: ArgumentArity.ZeroOrOne)
                {
                    ArgumentHelpName = "severity"
                }.FromAmong(SeverityLevels),
                new Option<string[]>(new[] { "--diagnostics" }, () => Array.Empty<string>(), Resources.A_space_separated_list_of_diagnostic_ids_to_use_as_a_filter_when_fixing_code_style_or_3rd_party_issues)
                {
                    AllowMultipleArgumentsPerToken = true
                },
                new Option<string[]>(new[] { "--include" }, () => Array.Empty<string>(), Resources.A_list_of_relative_file_or_folder_paths_to_include_in_formatting_All_files_are_formatted_if_empty)                {
                    AllowMultipleArgumentsPerToken = true
                },
                new Option<string[]>(new[] { "--exclude" }, () => Array.Empty<string>(), Resources.A_list_of_relative_file_or_folder_paths_to_exclude_from_formatting)                {
                    AllowMultipleArgumentsPerToken = true
                },
                new Option<bool>(new[] { "--check" }, Resources.Formats_files_without_saving_changes_to_disk_Terminates_with_a_non_zero_exit_code_if_any_files_were_formatted),
                new Option(new[] { "--report" }, Resources.Accepts_a_file_path_which_if_provided_will_produce_a_format_report_json_file_in_the_given_directory, argumentType: typeof(string), arity: ArgumentArity.ZeroOrOne)
                {
                    ArgumentHelpName = "report-path"
                }.LegalFilePathsOnly(),
                new Option<string>(new[] { "--verbosity", "-v" }, Resources.Set_the_verbosity_level_Allowed_values_are_quiet_minimal_normal_detailed_and_diagnostic).FromAmong(VerbosityLevels),
                new Option<bool>(new[] { "--include-generated" }, Resources.Include_generated_code_files_in_formatting_operations)
                {
                    IsHidden = true
                },
                new Option(new[] { "--binarylog" }, Resources.Log_all_project_or_solution_load_information_to_a_binary_log_file, argumentType: typeof(string), arity: ArgumentArity.ZeroOrOne)
                {
                    ArgumentHelpName = "binary-log-path"
                }.LegalFilePathsOnly(),
            };

            rootCommand.Description = "dotnet-format";
            rootCommand.AddValidator(EnsureFolderNotSpecifiedWhenFixingStyle);
            rootCommand.AddValidator(EnsureFolderNotSpecifiedWhenFixingAnalyzers);
            rootCommand.AddValidator(EnsureFolderNotSpecifiedWithNoRestore);
            rootCommand.AddValidator(EnsureFolderNotSpecifiedWhenLoggingBinlog);

            return rootCommand;
        }

        internal static string? EnsureFolderNotSpecifiedWhenFixingAnalyzers(CommandResult symbolResult)
        {
            var folder = symbolResult.GetValueForOption<bool>("--folder");
            var fixAnalyzers = symbolResult.GetOptionResult("--fix-analyzers");
            return folder && fixAnalyzers != null
                ? Resources.Cannot_specify_the_folder_option_when_running_analyzers
                : null;
        }

        internal static string? EnsureFolderNotSpecifiedWhenFixingStyle(CommandResult symbolResult)
        {
            var folder = symbolResult.GetValueForOption<bool>("--folder");
            var fixStyle = symbolResult.GetOptionResult("--fix-style");
            return folder && fixStyle != null
                ? Resources.Cannot_specify_the_folder_option_when_fixing_style
                : null;
        }

        internal static string? EnsureFolderNotSpecifiedWithNoRestore(CommandResult symbolResult)
        {
            var folder = symbolResult.GetValueForOption<bool>("--folder");
            var noRestore = symbolResult.GetOptionResult("--no-restore");
            return folder && noRestore != null
                ? Resources.Cannot_specify_the_folder_option_with_no_restore
                : null;
        }

        internal static string? EnsureFolderNotSpecifiedWhenLoggingBinlog(CommandResult symbolResult)
        {
            var folder = symbolResult.GetValueForOption<bool>("--folder");
            var binarylog = symbolResult.GetOptionResult("--binarylog");
            return folder && binarylog is not null && !binarylog.IsImplicit
                ? Resources.Cannot_specify_the_folder_option_when_writing_a_binary_log
                : null;
        }
    }
}
