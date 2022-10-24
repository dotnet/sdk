// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;

namespace Microsoft.TemplateEngine.Authoring.CLI.Commands.Verify
{
    internal class VerifyCommand : ExecutableCommand<VerifyCommandArgs>
    {
        private const string CommandName = "verify";

        private readonly Argument<string> _templateNameArgument = new("template-short-name")
        {
            Description = LocalizableStrings.command_verify_help_templateName_description,
            // 0 for case where only path is specified
            Arity = new ArgumentArity(1, 1)
        };

        private readonly Option<string> _remainingArguments = new Option<string>("--template-args")
        {
            Description = "Template specific arguments - all joined into single enquoted string. Any needed quotations of actual arguments has to be escaped.",
            Arity = new ArgumentArity(0, 1)
        };

        private readonly Option<string> _templatePathOption = new(new[] { "-p", "--template-path" })
        {
            Description = LocalizableStrings.command_verify_help_templatePath_description,
        };

        private readonly Option<string> _newCommandPathOption = new("--new-command-assembly")
        {
            Description = LocalizableStrings.command_verify_help_newCommandPath_description,
            //TODO: do we have better way of distinguishing options that might rarely be needed?
            // if not - we should probably add a link to more detailed help in the command description (mentioning that online help has additional options)
            IsHidden = true
        };

        private readonly Option<string> _templateOutputPathOption = new(new[] { "-o", "--output" })
        {
            Description = LocalizableStrings.command_verify_help_outputPath_description,
        };

        private readonly Option<string> _snapshotsDirectoryOption = new(new[] { "-d", "--snapshots-directory" })
        {
            Description = LocalizableStrings.command_verify_help_snapshotsDirPath_description,
        };

        private readonly Option<string> _scenarioNameOption = new(new[] { "--scenario-name" })
        {
            Description = LocalizableStrings.command_verify_help_scenarioName_description,
        };

        private readonly Option<bool> _disableDiffToolOption = new("--disable-diff-tool")
        {
            Description = LocalizableStrings.command_verify_help_disableDiffTool_description,
        };

        private readonly Option<bool> _disableDefaultExcludePatternsOption = new("--disable-default-exclude-patterns")
        {
            Description = LocalizableStrings.command_verify_help_disableDefaultExcludes_description,
        };

        private readonly Option<IEnumerable<string>> _excludePatternOption = new("--exclude-pattern")
        {
            Description = LocalizableStrings.command_verify_help_customExcludes_description,
            Arity = new ArgumentArity(0, 999)
        };

        private readonly Option<IEnumerable<string>> _includePatternOption = new("--include-pattern")
        {
            Description = LocalizableStrings.command_verify_help_customIncludes_description,
            Arity = new ArgumentArity(0, 999)
        };

        private readonly Option<bool> _verifyCommandOutputOption = new("--verify-std")
        {
            Description = LocalizableStrings.command_verify_help_verifyOutputs_description,
        };

        private readonly Option<bool> _isCommandExpectedToFailOption = new("--fail-expected")
        {
            Description = LocalizableStrings.command_verify_help_expectFailure_description,
        };

        private readonly Option<IEnumerable<UniqueForOption>> _uniqueForOption = new("--unique-for")
        {
            Description = LocalizableStrings.command_verify_help_uniqueFor_description,
            Arity = new ArgumentArity(0, 999),
            AllowMultipleArgumentsPerToken = true,
        };

        public VerifyCommand(ILoggerFactory loggerFactory)
            : base(CommandName, LocalizableStrings.command_verify_help_description, loggerFactory)
        {
            AddArgument(_templateNameArgument);
            AddOption(_remainingArguments);
            AddOption(_templatePathOption);
            AddOption(_newCommandPathOption);
            AddOption(_templateOutputPathOption);
            AddOption(_snapshotsDirectoryOption);
            AddOption(_scenarioNameOption);
            AddOption(_disableDiffToolOption);
            AddOption(_disableDefaultExcludePatternsOption);
            AddOption(_excludePatternOption);
            AddOption(_includePatternOption);
            AddOption(_verifyCommandOutputOption);
            AddOption(_isCommandExpectedToFailOption);
            FromAmongCaseInsensitive(
                _uniqueForOption,
                Enum.GetNames(typeof(UniqueForOption))
                    .Where(v => !v.Equals(UniqueForOption.None.ToString(), StringComparison.OrdinalIgnoreCase))
                    .ToArray());
            AddOption(_uniqueForOption);
        }

        internal static VerifyCommandArgs ExtractArguments(VerifyCommand verifyCommand, ParseResult parseResult)
        {
            return new VerifyCommandArgs(
                templateName: parseResult.GetValueForArgument(verifyCommand._templateNameArgument),
                templateSpecificArgs: parseResult.GetValueForOption(verifyCommand._remainingArguments),
                templatePath: parseResult.GetValueForOption(verifyCommand._templatePathOption),
                dotnetNewCommandAssemblyPath: parseResult.GetValueForOption(verifyCommand._newCommandPathOption),
                snapshotsDirectory: parseResult.GetValueForOption(verifyCommand._snapshotsDirectoryOption),
                scenarioDistinguisher: parseResult.GetValueForOption(verifyCommand._scenarioNameOption),
                outputDirectory: parseResult.GetValueForOption(verifyCommand._templateOutputPathOption),
                disableDiffTool: parseResult.GetValueForOption(verifyCommand._disableDiffToolOption),
                disableDefaultVerificationExcludePatterns: parseResult.GetValueForOption(verifyCommand._disableDefaultExcludePatternsOption),
                verificationExcludePatterns: parseResult.GetValueForOption(verifyCommand._excludePatternOption),
                verificationIncludePatterns: parseResult.GetValueForOption(verifyCommand._includePatternOption),
                verifyCommandOutput: parseResult.GetValueForOption(verifyCommand._verifyCommandOutputOption),
                isCommandExpectedToFail: parseResult.GetValueForOption(verifyCommand._isCommandExpectedToFailOption),
                uniqueForOptions: parseResult.GetValueForOption(verifyCommand._uniqueForOption));
        }

        protected override async Task<int> ExecuteAsync(VerifyCommandArgs args, CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Running the verification of {templateName}.", args.TemplateName);

            try
            {
                VerificationEngine engine = new VerificationEngine(LoggerFactory ?? NullLoggerFactory.Instance);
                TemplateVerifierOptions options = new(templateName: args.TemplateName)
                {
                    TemplatePath = args.TemplatePath,
                    TemplateSpecificArgs = args.TemplateSpecificArgs,
                    DisableDiffTool = args.DisableDiffTool,
                    DisableDefaultVerificationExcludePatterns = args.DisableDefaultVerificationExcludePatterns,
                    VerificationExcludePatterns = args.VerificationExcludePatterns,
                    VerificationIncludePatterns = args.VerificationIncludePatterns,
                    DotnetNewCommandAssemblyPath = args.DotnetNewCommandAssemblyPath,
                    SnapshotsDirectory = args.SnapshotsDirectory,
                    ScenarioName = args.ScenarioDistinguisher,
                    OutputDirectory = args.OutputDirectory,
                    VerifyCommandOutput = args.VerifyCommandOutput,
                    IsCommandExpectedToFail = args.IsCommandExpectedToFail,
                    UniqueFor = args.UniqueFor,
                    DoNotPrependCallerMethodNameToScenarioName = true
                };
                await engine.Execute(
                    options,
                    cancellationToken,
                    // We explicitly pass a path - so that the engine then process it and gets the current executing dir
                    // and treats it as a code base of caller of API (as in case of CLI usage we do not want to store
                    // expectation files in CLI sources dir)
                    Path.Combine(Environment.CurrentDirectory, "_"))
                    .ConfigureAwait(false);
                return 0;
            }
            catch (Exception e)
            {
                Logger.LogError(LocalizableStrings.command_verify_error_failed);
                Logger.LogError(e.Message);
                TemplateVerificationException? ex = e as TemplateVerificationException;
                return (int)(ex?.TemplateVerificationErrorCode ?? TemplateVerificationErrorCode.InternalError);
            }
        }

        protected override BinderBase<VerifyCommandArgs> GetModelBinder() => new VerifyModelBinder(this);

        /// <summary>
        /// Case insensitive version for <see cref="OptionExtensions.FromAmong{TOption}(TOption, string[])"/>.
        /// </summary>
        private static void FromAmongCaseInsensitive(Option option, string[]? allowedValues = null, string? allowedHiddenValue = null)
        {
            allowedValues ??= Array.Empty<string>();
            option.AddValidator(optionResult => ValidateAllowedValues(optionResult, allowedValues, allowedHiddenValue));
            option.AddCompletions(allowedValues);
        }

        private static void ValidateAllowedValues(OptionResult optionResult, string[] allowedValues, string? allowedHiddenValue = null)
        {
            var invalidArguments = optionResult.Tokens.Where(token => !allowedValues.Append(allowedHiddenValue).Contains(token.Value, StringComparer.OrdinalIgnoreCase)).ToList();
            if (invalidArguments.Any())
            {
                optionResult.ErrorMessage = string.Format(
                    LocalizableStrings.command_verify_error_unrecognizedArguments,
                    string.Join(", ", invalidArguments.Select(arg => $"'{arg.Value}'")),
                    string.Join(", ", allowedValues.Select(allowedValue => $"'{allowedValue}'")));
            }
        }

        private class VerifyModelBinder : BinderBase<VerifyCommandArgs>
        {
            private readonly VerifyCommand _verifyCommand;

            internal VerifyModelBinder(VerifyCommand verifyCommand)
            {
                _verifyCommand = verifyCommand;
            }

            protected override VerifyCommandArgs GetBoundValue(BindingContext bindingContext)
            {
                return ExtractArguments(_verifyCommand, bindingContext.ParseResult);
            }
        }
    }
}
