// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
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
            string? fixStyle,
            string? fixAnalyzers,
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
                new Argument<string?>("workspace")
                {
                    Arity = ArgumentArity.ZeroOrOne,
                    Description = Resources.A_path_to_a_solution_file_a_project_file_or_a_folder_containing_a_solution_or_project_file_If_a_path_is_not_specified_then_the_current_directory_is_used
                }.LegalFilePathsOnly(),
                new Option(new[] { "--no-restore" }, Resources.Doesnt_execute_an_implicit_restore_before_formatting),
                new Option(new[] { "--folder", "-f" }, Resources.Whether_to_treat_the_workspace_argument_as_a_simple_folder_of_files),
                new Option(new[] { "--fix-whitespace", "-w" }, Resources.Run_whitespace_formatting_Run_by_default_when_not_applying_fixes),
                new Option(new[] { "--fix-style", "-s" }, Resources.Run_code_style_analyzers_and_apply_fixes)
                {
                    Argument = new Argument<string?>("severity") { Arity = ArgumentArity.ZeroOrOne }.FromAmong(SeverityLevels)
                },
                new Option(new[] { "--fix-analyzers", "-a" }, Resources.Run_3rd_party_analyzers_and_apply_fixes)
                {
                    Argument = new Argument<string?>("severity") { Arity = ArgumentArity.ZeroOrOne }.FromAmong(SeverityLevels)
                },
                new Option(new[] { "--diagnostics" }, Resources.A_space_separated_list_of_diagnostic_ids_to_use_as_a_filter_when_fixing_code_style_or_3rd_party_issues)
                {
                    Argument = new Argument<string[]>(() => Array.Empty<string>())
                },
                new Option(new[] { "--include" }, Resources.A_list_of_relative_file_or_folder_paths_to_include_in_formatting_All_files_are_formatted_if_empty)
                {
                    Argument = new Argument<string[]>(() => Array.Empty<string>())
                },
                new Option(new[] { "--exclude" }, Resources.A_list_of_relative_file_or_folder_paths_to_exclude_from_formatting)
                {
                    Argument = new Argument<string[]>(() => Array.Empty<string>())
                },
                new Option(new[] { "--check" }, Resources.Formats_files_without_saving_changes_to_disk_Terminates_with_a_non_zero_exit_code_if_any_files_were_formatted),
                new Option(new[] { "--report" }, Resources.Accepts_a_file_path_which_if_provided_will_produce_a_format_report_json_file_in_the_given_directory)
                {
                    Argument = new Argument<string?>(() => null) { Name = "report-path" }.LegalFilePathsOnly()
                },
                new Option(new[] { "--verbosity", "-v" }, Resources.Set_the_verbosity_level_Allowed_values_are_quiet_minimal_normal_detailed_and_diagnostic)
                {
                    Argument = new Argument<string?>() { Arity = ArgumentArity.ExactlyOne }.FromAmong(VerbosityLevels)
                },
                new Option(new[] { "--include-generated" }, Resources.Include_generated_code_files_in_formatting_operations)
                {
                    IsHidden = true
                },
                new Option(new[] { "--binarylog" }, Resources.Log_all_project_or_solution_load_information_to_a_binary_log_file)
                {
                    Argument = new Argument<string?>(() => null) { Name = "binary-log-path", Arity = ArgumentArity.ZeroOrOne }.LegalFilePathsOnly()
                },
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
            var folder = symbolResult.ValueForOption<bool>("--folder");
            var fixAnalyzers = symbolResult.OptionResult("--fix-analyzers");
            return folder && fixAnalyzers != null
                ? Resources.Cannot_specify_the_folder_option_when_running_analyzers
                : null;
        }

        internal static string? EnsureFolderNotSpecifiedWhenFixingStyle(CommandResult symbolResult)
        {
            var folder = symbolResult.ValueForOption<bool>("--folder");
            var fixStyle = symbolResult.OptionResult("--fix-style");
            return folder && fixStyle != null
                ? Resources.Cannot_specify_the_folder_option_when_fixing_style
                : null;
        }

        internal static string? EnsureFolderNotSpecifiedWithNoRestore(CommandResult symbolResult)
        {
            var folder = symbolResult.ValueForOption<bool>("--folder");
            var noRestore = symbolResult.OptionResult("--no-restore");
            return folder && noRestore != null
                ? Resources.Cannot_specify_the_folder_option_with_no_restore
                : null;
        }

        internal static string? EnsureFolderNotSpecifiedWhenLoggingBinlog(CommandResult symbolResult)
        {
            var folder = symbolResult.ValueForOption<bool>("--folder");
            var binarylog = symbolResult.OptionResult("--binarylog");
            return folder && binarylog is not null && !binarylog.IsImplicit
                ? Resources.Cannot_specify_the_folder_option_when_writing_a_binary_log
                : null;
        }

        internal static bool WasOptionUsed(this ParseResult result, params string[] aliases)
        {
            return result.Tokens
                .Where(token => token.Type == TokenType.Option)
                .Any(token => aliases.Contains(token.Value));
        }
    }
}
