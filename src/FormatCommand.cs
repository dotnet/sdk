// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;

namespace Microsoft.CodeAnalysis.Tools
{
    internal static class FormatCommand
    {
        internal static RootCommand CreateCommandLineOptions()
        {
            var rootCommand = new RootCommand
            {
                new Argument<string>("project")
                {
                    Arity = ArgumentArity.ZeroOrOne,
                    Description = Resources.The_solution_or_project_file_to_operate_on_If_a_file_is_not_specified_the_command_will_search_the_current_directory_for_one
                },
                new Option(new[] { "--folder", "-f" }, Resources.Whether_to_treat_the_project_path_as_a_folder_of_files)
                {
                    Argument = new Argument<string?>(() => null) { Arity = ArgumentArity.ZeroOrOne },
                },
                new Option(new[] { "--workspace", "-w" }, Resources.The_solution_or_project_file_to_operate_on_If_a_file_is_not_specified_the_command_will_search_the_current_directory_for_one)
                {
                    Argument = new Argument<string?>(() => null),
                    IsHidden = true
                },
                new Option(new[] { "--fix-style", "-fs" }, Resources.Run_code_style_analyzer_and_apply_fixes)
                {
                    Argument = new Argument<bool>()
                },
                new Option(new[] { "--include", "--files" }, Resources.A_list_of_relative_file_or_folder_paths_to_include_in_formatting_All_files_are_formatted_if_empty)
                {
                    Argument = new Argument<string[]>(() => Array.Empty<string>())
                },
                new Option(new[] { "--exclude" }, Resources.A_list_of_relative_file_or_folder_paths_to_exclude_from_formatting)
                {
                    Argument = new Argument<string[]>(() => Array.Empty<string>())
                },
                new Option(new[] { "--check", "--dry-run" }, Resources.Formats_files_without_saving_changes_to_disk_Terminates_with_a_non_zero_exit_code_if_any_files_were_formatted)
                {
                    Argument = new Argument<bool>()
                },
                new Option(new[] { "--report" }, Resources.Accepts_a_file_path_which_if_provided_will_produce_a_format_report_json_file_in_the_given_directory)
                {
                    Argument = new Argument<string?>(() => null)
                },
                new Option(new[] { "--verbosity", "-v" }, Resources.Set_the_verbosity_level_Allowed_values_are_quiet_minimal_normal_detailed_and_diagnostic)
                {
                    Argument = new Argument<string?>() { Arity = ArgumentArity.ExactlyOne }
                },
                new Option(new[] { "--include-generated" }, Resources.Include_generated_code_files_in_formatting_operations)
                {
                    Argument = new Argument<bool>(),
                    IsHidden = true
                },
            };

            rootCommand.Description = "dotnet-format";
            rootCommand.AddValidator(ValidateProjectArgumentAndWorkspace);
            rootCommand.AddValidator(ValidateProjectArgumentAndFolder);
            rootCommand.AddValidator(ValidateWorkspaceAndFolder);

            return rootCommand;
        }

        private static string? ValidateProjectArgumentAndWorkspace(CommandResult symbolResult)
        {
            try
            {
                var project = symbolResult.GetArgumentValueOrDefault<string>("project");
                var workspace = symbolResult.ValueForOption<string>("workspace");

                if (!string.IsNullOrEmpty(project) && !string.IsNullOrEmpty(workspace))
                {
                    return Resources.Cannot_specify_both_project_argument_and_workspace_option;
                }
            }
            catch (InvalidOperationException) // Parsing of arguments failed. This will be reported later.
            {
            }

            return null;
        }

        private static string? ValidateProjectArgumentAndFolder(CommandResult symbolResult)
        {
            try
            {
                var project = symbolResult.GetArgumentValueOrDefault<string>("project");
                var folder = symbolResult.ValueForOption<string>("folder");

                if (!string.IsNullOrEmpty(project) && !string.IsNullOrEmpty(folder))
                {
                    return Resources.Cannot_specify_both_project_argument_and_folder_options;
                }
            }
            catch (InvalidOperationException) // Parsing of arguments failed. This will be reported later.
            {
            }

            return null;
        }

        private static string? ValidateWorkspaceAndFolder(CommandResult symbolResult)
        {
            try
            {
                var workspace = symbolResult.ValueForOption<string>("workspace");
                var folder = symbolResult.ValueForOption<string>("folder");


                if (!string.IsNullOrEmpty(workspace) && !string.IsNullOrEmpty(folder))
                {
                    return Resources.Cannot_specify_both_folder_and_workspace_options;
                }
            }
            catch (InvalidOperationException) // Parsing of arguments failed. This will be reported later.
            {
            }

            return null;
        }

        internal static bool WasOptionUsed(this ParseResult result, params string[] aliases)
        {
            return result.Tokens
                .Where(token => token.Type == TokenType.Option)
                .Any(token => aliases.Contains(token.Value));
        }
    }
}
