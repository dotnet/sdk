// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class NewCommand : BaseCommand<NewCommandArgs>
    {
        private static readonly IReadOnlyList<FilterOptionDefinition> LegacyFilterDefinitions = new List<FilterOptionDefinition>()
        {
            FilterOptionDefinition.AuthorFilter,
            FilterOptionDefinition.BaselineFilter,
            FilterOptionDefinition.LanguageFilter,
            FilterOptionDefinition.TypeFilter,
            FilterOptionDefinition.TagFilter,
            FilterOptionDefinition.PackageFilter
        };

        internal IEnumerable<Option> LegacyOptions
        {
            get
            {
                yield return InteractiveOption;
                yield return AddSourceOption;
                yield return ColumnsAllOption;
                yield return ColumnsOption;
                foreach (Option filter in LegacyFilters.Values)
                {
                    yield return filter;
                }
            }
        }

        internal Option<bool> InteractiveOption { get; } = SharedOptionsFactory.CreateInteractiveOption().AsHidden();

        internal Option<string[]> AddSourceOption { get; } = SharedOptionsFactory.CreateAddSourceOption().AsHidden().DisableAllowMultipleArgumentsPerToken();

        internal Option<bool> ColumnsAllOption { get; } = SharedOptionsFactory.CreateColumnsAllOption().AsHidden();

        internal Option<string[]> ColumnsOption { get; } = SharedOptionsFactory.CreateColumnsOption().AsHidden().DisableAllowMultipleArgumentsPerToken();

        internal IReadOnlyDictionary<FilterOptionDefinition, Option> LegacyFilters { get; private set; } = new Dictionary<FilterOptionDefinition, Option>();

        internal void AddNoLegacyUsageValidators(Command command, params Symbol[] except)
        {
            IEnumerable<Option> optionsToVerify = LegacyFilters.Values.Concat(new Option[] { ColumnsAllOption, ColumnsOption, InteractiveOption, AddSourceOption });
            IEnumerable<Argument> argumentsToVerify = new Argument[] { ShortNameArgument, RemainingArguments };

            foreach (Option option in optionsToVerify)
            {
                if (!except.Contains(option))
                {
                    command.AddValidator(symbolResult => ValidateOptionUsage(symbolResult, option));
                }
            }

            foreach (Argument argument in argumentsToVerify)
            {
                if (!except.Contains(argument))
                {
                    command.AddValidator(symbolResult => ValidateArgumentUsage(symbolResult, argument));
                }
            }
        }

        internal void ValidateShortNameArgumentIsNotUsed(CommandResult commandResult)
        {
            ValidateArgumentUsage(commandResult, ShortNameArgument);
        }

        internal void ValidateArgumentsAreNotUsed(CommandResult commandResult)
        {
            ValidateArgumentUsage(commandResult, ShortNameArgument, RemainingArguments);
        }

        private static void ValidateOptionUsage(CommandResult commandResult, Option option)
        {
            OptionResult? optionResult = commandResult.Parent?.Children.FirstOrDefault(symbol => symbol.Symbol == option) as OptionResult;
            if (optionResult != null)
            {
                List<string> wrongTokens = new List<string>();
                if (optionResult.Token is { } && !string.IsNullOrWhiteSpace(optionResult.Token.Value))
                {
                    wrongTokens.Add($"'{optionResult.Token.Value}'");
                }
                foreach (var token in optionResult.Tokens)
                {
                    if (token is { } t && !string.IsNullOrWhiteSpace(t.Value))
                    {
                        wrongTokens.Add($"'{t.Value}'");
                    }
                }
                //Unrecognized command or argument(s): {0}
                commandResult.ErrorMessage = string.Format(LocalizableStrings.Commands_Validator_WrongTokens, string.Join(",", wrongTokens));
            }
        }

        private static void ValidateArgumentUsage(CommandResult commandResult, params Argument[] arguments)
        {
            List<string> wrongTokens = new List<string>();
            foreach (Argument argument in arguments)
            {
                var newCommandArgument = commandResult.Parent?.Children.FirstOrDefault(symbol => symbol.Symbol == argument) as ArgumentResult;
                if (newCommandArgument == null)
                {
                    continue;
                }
                foreach (var token in newCommandArgument.Tokens)
                {
                    if (!string.IsNullOrWhiteSpace(token.Value))
                    {
                        wrongTokens.Add($"'{token.Value}'");
                    }
                }
            }
            if (wrongTokens.Any())
            {
                //Unrecognized command or argument(s): {0}
                commandResult.ErrorMessage = string.Format(LocalizableStrings.Commands_Validator_WrongTokens, string.Join(",", wrongTokens));
            }
        }

        private void BuildLegacySymbols(Func<ParseResult, ITemplateEngineHost> hostBuilder, Func<ParseResult, ITelemetryLogger> telemetryLoggerBuilder, NewCommandCallbacks callbacks)
        {
            this.AddArgument(ShortNameArgument);
            this.AddArgument(RemainingArguments);

            //legacy options
            Dictionary<FilterOptionDefinition, Option> options = new Dictionary<FilterOptionDefinition, Option>();
            foreach (var filterDef in LegacyFilterDefinitions)
            {
                options[filterDef] = filterDef.OptionFactory().AsHidden();
                this.AddOption(options[filterDef]);
            }
            LegacyFilters = options;

            this.AddOption(InteractiveOption);
            this.AddOption(AddSourceOption);
            this.AddOption(ColumnsAllOption);
            this.AddOption(ColumnsOption);

            this.TreatUnmatchedTokensAsErrors = true;

            this.Add(new LegacyInstallCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new LegacyUninstallCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new LegacyUpdateCheckCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new LegacyUpdateApplyCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new LegacySearchCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new LegacyListCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new LegacyAliasAddCommand(hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new LegacyAliasShowCommand(hostBuilder, telemetryLoggerBuilder, callbacks));
        }
    }
}

