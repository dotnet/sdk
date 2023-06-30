﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.TabularOutput;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class InstantiateCommand : BaseCommand<InstantiateCommandArgs>, ICustomHelp
    {
        internal InstantiateCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(hostBuilder, "create", SymbolStrings.Command_Instantiate_Description)
        {
            this.AddArgument(ShortNameArgument);
            this.AddArgument(RemainingArguments);

            this.AddOption(SharedOptions.OutputOption);
            this.AddOption(SharedOptions.NameOption);
            this.AddOption(SharedOptions.DryRunOption);
            this.AddOption(SharedOptions.ForceOption);
            this.AddOption(SharedOptions.NoUpdateCheckOption);
            this.AddOption(SharedOptions.ProjectPathOption);

            parentCommand.AddNoLegacyUsageValidators(this);
            this.AddValidator(symbolResult => parentCommand.ValidateOptionUsage(symbolResult, SharedOptions.OutputOption));
            this.AddValidator(symbolResult => parentCommand.ValidateOptionUsage(symbolResult, SharedOptions.NameOption));
            this.AddValidator(symbolResult => parentCommand.ValidateOptionUsage(symbolResult, SharedOptions.DryRunOption));
            this.AddValidator(symbolResult => parentCommand.ValidateOptionUsage(symbolResult, SharedOptions.ForceOption));
            this.AddValidator(symbolResult => parentCommand.ValidateOptionUsage(symbolResult, SharedOptions.NoUpdateCheckOption));
            this.AddValidator(symbolResult => parentCommand.ValidateOptionUsage(symbolResult, SharedOptions.ProjectPathOption));
        }

        internal static Argument<string> ShortNameArgument { get; } = new Argument<string>("template-short-name")
        {
            Description = SymbolStrings.Command_Instantiate_Argument_ShortName,
            Arity = new ArgumentArity(0, 1)
        };

        internal Argument<string[]> RemainingArguments { get; } = new Argument<string[]>("template-args")
        {
            Description = SymbolStrings.Command_Instantiate_Argument_TemplateOptions,
            Arity = new ArgumentArity(0, 999)
        };

        internal IReadOnlyList<Option> PassByOptions { get; } = new Option[]
        {
            SharedOptions.ForceOption,
            SharedOptions.NameOption,
            SharedOptions.DryRunOption,
            SharedOptions.NoUpdateCheckOption
        };

        internal static Task<NewCommandStatus> ExecuteAsync(
            NewCommandArgs newCommandArgs,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            InvocationContext context)
        {
            return ExecuteIntAsync(InstantiateCommandArgs.FromNewCommandArgs(newCommandArgs), environmentSettings, templatePackageManager, context);
        }

        internal static async Task<IEnumerable<TemplateGroup>> GetTemplateGroupsAsync(
            TemplatePackageManager templatePackageManager,
            HostSpecificDataLoader hostSpecificDataLoader,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ITemplateInfo> templates = await templatePackageManager.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
            return TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templates, hostSpecificDataLoader));
        }

        internal static HashSet<TemplateCommand> GetTemplateCommand(
                InstantiateCommandArgs args,
                IEngineEnvironmentSettings environmentSettings,
                TemplatePackageManager templatePackageManager,
                TemplateGroup templateGroup)
        {
            //groups templates in the group by precedence
            foreach (IGrouping<int, CliTemplateInfo> templateGrouping in templateGroup.Templates.GroupBy(g => g.Precedence).OrderByDescending(g => g.Key))
            {
                HashSet<TemplateCommand> candidates = ReparseForTemplate(
                    args,
                    environmentSettings,
                    templatePackageManager,
                    templateGroup,
                    templateGrouping,
                    out bool languageOptionSpecified);

                //if no candidates continue with next precedence
                if (!candidates.Any())
                {
                    continue;
                }
                //if language option is not specified, we do not need to do reparsing for default language
                if (languageOptionSpecified || string.IsNullOrWhiteSpace(environmentSettings.GetDefaultLanguage()))
                {
                    return candidates;
                }

                // try to reparse for default language
                return ReparseForDefaultLanguage(
                    args,
                    environmentSettings,
                    templatePackageManager,
                    templateGroup,
                    candidates);
            }
            return new HashSet<TemplateCommand>();
        }

        internal static void HandleNoMatchingTemplateGroup(InstantiateCommandArgs instantiateArgs, IEnumerable<TemplateGroup> templateGroups, IReporter reporter)
        {
            reporter.WriteLine(
                string.Format(LocalizableStrings.InstantiateCommand_Info_NoMatchingTemplatesSubCommands, instantiateArgs.ShortName).Bold().Red());

            SuggestTypoCorrections(instantiateArgs, templateGroups, reporter);
            reporter.WriteLine();

            WriteListCommandExample(instantiateArgs, reporter);
            WriteSearchCommandExample(instantiateArgs, reporter);
            reporter.WriteLine();
        }

        internal static NewCommandStatus HandleAmbiguousTemplateGroup(
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            IEnumerable<TemplateGroup> templateGroups,
            IReporter reporter,
            CancellationToken cancellationToken = default)
        {
            IEnvironment environment = environmentSettings.Environment;
            reporter.WriteLine(LocalizableStrings.AmbiguousTemplatesHeader.Bold().Red());
            TabularOutput<TemplateGroup> formatter =
                TabularOutput.TabularOutput
                    .For(
                        new TabularOutputSettings(environment),
                        templateGroups)
                    .DefineColumn(t => t.GroupIdentity ?? t.Templates[0].Identity, out object? identityColumn, LocalizableStrings.ColumnNameIdentity, showAlways: true)
                    .DefineColumn(t => t.Name, LocalizableStrings.ColumnNameTemplateName, shrinkIfNeeded: true, minWidth: 15, showAlways: true)
                    .DefineColumn(t => string.Join(",", t.ShortNames), LocalizableStrings.ColumnNameShortName, showAlways: true)
                    .DefineColumn(t => string.Join(",", t.Languages), LocalizableStrings.ColumnNameLanguage, showAlways: true)
                    .DefineColumn(t => string.Join(",", t.Authors), LocalizableStrings.ColumnNameAuthor, showAlways: true, shrinkIfNeeded: true, minWidth: 10)
                    .DefineColumn(t => Task.Run(() => GetTemplatePackagesList(t)).GetAwaiter().GetResult(), LocalizableStrings.ColumnNamePackage, showAlways: true)
                    .OrderBy(identityColumn, StringComparer.CurrentCultureIgnoreCase);

            reporter.WriteLine(formatter.Layout().Bold().Red());
            reporter.WriteLine(LocalizableStrings.AmbiguousTemplatesMultiplePackagesHint.Bold().Red());
            return NewCommandStatus.NotFound;

            async Task<string> GetTemplatePackagesList(TemplateGroup templateGroup)
            {
                try
                {
                    IReadOnlyList<IManagedTemplatePackage> templatePackages =
                        await templateGroup.GetManagedTemplatePackagesAsync(templatePackageManager, cancellationToken).ConfigureAwait(false);
                    return string.Join(environment.NewLine, templatePackages.Select(templatePackage => templatePackage.Identifier));
                }
                catch (Exception ex)
                {
                    environmentSettings.Host.Logger.LogWarning(LocalizableStrings.InstantiateCommand_Warning_FailedToGetTemplatePackageForTemplateGroup, templateGroup.GroupIdentity);
                    environmentSettings.Host.Logger.LogDebug("Details: {exception}.", ex);
                    return string.Empty;
                }
            }
        }

        protected override async Task<NewCommandStatus> ExecuteAsync(
            InstantiateCommandArgs instantiateArgs,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            InvocationContext context)
        {
            NewCommandStatus status = await ExecuteIntAsync(instantiateArgs, environmentSettings, templatePackageManager, context).ConfigureAwait(false);
            await CheckTemplatesWithSubCommandName(instantiateArgs, templatePackageManager, context.GetCancellationToken()).ConfigureAwait(false);
            return status;
        }

        protected override InstantiateCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);

        private static async Task<NewCommandStatus> ExecuteIntAsync(
            InstantiateCommandArgs instantiateArgs,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            InvocationContext context)
        {
            CancellationToken cancellationToken = context.GetCancellationToken();
            HostSpecificDataLoader hostSpecificDataLoader = new(environmentSettings);
            if (string.IsNullOrWhiteSpace(instantiateArgs.ShortName))
            {
                TemplateListCoordinator templateListCoordinator = new(
                    environmentSettings,
                    templatePackageManager,
                    hostSpecificDataLoader);

                return await templateListCoordinator.DisplayCommandDescriptionAsync(instantiateArgs, cancellationToken).ConfigureAwait(false);
            }

            IEnumerable<TemplateGroup> allTemplateGroups = await GetTemplateGroupsAsync(
                templatePackageManager,
                hostSpecificDataLoader,
                cancellationToken).ConfigureAwait(false);

            IEnumerable<TemplateGroup> selectedTemplateGroups = allTemplateGroups.Where(template => template.ShortNames.Contains(instantiateArgs.ShortName));

            if (!selectedTemplateGroups.Any())
            {
                HandleNoMatchingTemplateGroup(instantiateArgs, allTemplateGroups, Reporter.Error);
                return NewCommandStatus.NotFound;
            }
            if (selectedTemplateGroups.Count() > 1)
            {
                return HandleAmbiguousTemplateGroup(environmentSettings, templatePackageManager, selectedTemplateGroups, Reporter.Error, cancellationToken);
            }
            return await HandleTemplateInstantiationAsync(
                instantiateArgs,
                environmentSettings,
                templatePackageManager,
                selectedTemplateGroups.Single(),
                cancellationToken).ConfigureAwait(false);
        }

        private static NewCommandStatus HandleAmbiguousLanguage(
            IEngineEnvironmentSettings environmentSettings,
            IEnumerable<CliTemplateInfo> templates,
            IReporter reporter)
        {
            reporter.WriteLine(HelpStrings.TableHeader_AmbiguousTemplatesList);
            TemplateGroupDisplay.DisplayTemplateList(
                environmentSettings,
                templates,
                new TabularOutputSettings(environmentSettings.Environment),
                reporter);
            reporter.WriteLine(HelpStrings.Hint_AmbiguousLanguage);
            return NewCommandStatus.NotFound;
        }

        private static NewCommandStatus HandleAmbiguousType(
            IEngineEnvironmentSettings environmentSettings,
            IEnumerable<CliTemplateInfo> templates,
            IReporter reporter)
        {
            reporter.WriteLine(HelpStrings.TableHeader_AmbiguousTemplatesList);
            TemplateGroupDisplay.DisplayTemplateList(
                environmentSettings,
                templates,
                new TabularOutputSettings(
                    environmentSettings.Environment,
                    columnsToDisplay: new[] { TabularOutputSettings.ColumnNames.Type }),
                reporter);
            reporter.WriteLine(HelpStrings.Hint_AmbiguousType);
            return NewCommandStatus.NotFound;
        }

        private static async Task<NewCommandStatus> HandleTemplateInstantiationAsync(
            InstantiateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup,
            CancellationToken cancellationToken)
        {
            HashSet<TemplateCommand> candidates = GetTemplateCommand(args, environmentSettings, templatePackageManager, templateGroup);
            if (candidates.Count == 1)
            {
                TemplateCommand templateCommandToRun = candidates.Single();
                args.Command.AddCommand(templateCommandToRun);

                ParseResult updatedParseResult = args.ParseResult.Parser.Parse(args.ParseResult.Tokens.Select(t => t.Value).ToList());
                return await candidates.Single().InvokeAsync(updatedParseResult, cancellationToken).ConfigureAwait(false);
            }
            else if (candidates.Any())
            {
                return HandleAmbiguousResult(
                    environmentSettings,
                    templatePackageManager,
                    candidates.Select(c => c.Template),
                    Reporter.Error,
                    cancellationToken);
            }

            return HandleNoTemplateFoundResult(args, environmentSettings, templatePackageManager, templateGroup, Reporter.Error);
        }

        private static NewCommandStatus HandleAmbiguousResult(
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            IEnumerable<CliTemplateInfo> templates,
            IReporter reporter,
            CancellationToken cancellationToken = default)
        {
            if (!templates.Any(t => string.IsNullOrWhiteSpace(t.GetLanguage()))
                && !templates.AllAreTheSame(t => t.GetLanguage()))
            {
                return HandleAmbiguousLanguage(
                       environmentSettings,
                       templates,
                       Reporter.Error);
            }

            if (!templates.Any(t => string.IsNullOrWhiteSpace(t.GetTemplateType()))
            && !templates.AllAreTheSame(t => t.GetTemplateType()))
            {
                return HandleAmbiguousType(
                       environmentSettings,
                       templates,
                       Reporter.Error);
            }

            reporter.WriteLine(LocalizableStrings.AmbiguousTemplatesHeader.Bold().Red());
            IEnvironment environment = environmentSettings.Environment;
            TabularOutput<CliTemplateInfo> formatter =
                    TabularOutput.TabularOutput
                        .For(
                            new TabularOutputSettings(environment),
                            templates)
                        .DefineColumn(t => t.Identity, out object? identityColumn, LocalizableStrings.ColumnNameIdentity, showAlways: true)
                        .DefineColumn(t => t.Name, LocalizableStrings.ColumnNameTemplateName, shrinkIfNeeded: true, minWidth: 15, showAlways: true)
                        .DefineColumn(t => string.Join(",", t.ShortNameList), LocalizableStrings.ColumnNameShortName, showAlways: true)
                        .DefineColumn(t => t.GetLanguage() ?? string.Empty, LocalizableStrings.ColumnNameLanguage, showAlways: true)
                        .DefineColumn(t => t.Precedence.ToString(), out object? precedenceColumn, LocalizableStrings.ColumnNamePrecedence, showAlways: true)
                        .DefineColumn(t => t.Author ?? string.Empty, LocalizableStrings.ColumnNameAuthor, showAlways: true, shrinkIfNeeded: true, minWidth: 10)
                        .DefineColumn(t => Task.Run(() => GetTemplatePackage(t)).GetAwaiter().GetResult(), LocalizableStrings.ColumnNamePackage, showAlways: true)
                        .OrderBy(identityColumn, StringComparer.CurrentCultureIgnoreCase)
                        .OrderByDescending(precedenceColumn, new NullOrEmptyIsLastStringComparer());
            reporter.WriteLine(formatter.Layout().Bold().Red());

            reporter.WriteLine(LocalizableStrings.AmbiguousTemplatesMultiplePackagesHint.Bold().Red());
            if (templates.AllAreTheSame(t => t.MountPointUri))
            {
                string templatePackage = Task.Run(() => GetTemplatePackage(templates.First())).GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(templatePackage))
                {
                    reporter.WriteLine(string.Format(LocalizableStrings.AmbiguousTemplatesSamePackageHint, templatePackage).Bold().Red());
                }
            }
            return NewCommandStatus.NotFound;

            async Task<string> GetTemplatePackage(CliTemplateInfo template)
            {
                try
                {
                    IManagedTemplatePackage? templatePackage =
                        await template.GetManagedTemplatePackageAsync(templatePackageManager, cancellationToken).ConfigureAwait(false);
                    return templatePackage?.Identifier ?? string.Empty;
                }
                catch (Exception ex)
                {
                    environmentSettings.Host.Logger.LogWarning(LocalizableStrings.InstantiateCommand_Warning_FailedToGetTemplatePackageForTemplate, template.Identity);
                    environmentSettings.Host.Logger.LogDebug("Details: {exception}.", ex);
                    return string.Empty;
                }
            }
        }

        private static HashSet<TemplateCommand> ReparseForTemplate(
            InstantiateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup,
            IEnumerable<CliTemplateInfo> templatesToReparse,
            out bool languageOptionSpecified)
        {
            languageOptionSpecified = false;
            HashSet<TemplateCommand> candidates = new();
            foreach (CliTemplateInfo template in templatesToReparse)
            {
                if (ReparseForTemplate(args, environmentSettings, templatePackageManager, templateGroup, template) is (TemplateCommand command, ParseResult parseResult))
                {
                    languageOptionSpecified = command.LanguageOption != null
                        && parseResult.FindResultFor(command.LanguageOption) != null;
                    if (!parseResult.Errors.Any())
                    {
                        candidates.Add(command);
                    }
                }
            }
            return candidates;
        }

        private static HashSet<TemplateCommand> ReparseForDefaultLanguage(
            InstantiateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup,
            HashSet<TemplateCommand> candidates)
        {
            HashSet<TemplateCommand> languageAwareCandidates = new();
            foreach (TemplateCommand templateCommand in candidates)
            {
                if (ReparseForTemplate(
                    args,
                    environmentSettings,
                    templatePackageManager,
                    templateGroup,
                    templateCommand.Template,
                    validateDefaultLanguage: true) is (TemplateCommand command, ParseResult parseResult))
                {
                    if (!parseResult.Errors.Any())
                    {
                        languageAwareCandidates.Add(command);
                    }
                }
            }
            return languageAwareCandidates.Any()
                ? languageAwareCandidates
                : candidates;
        }

        private static (TemplateCommand? Command, ParseResult? ParseResult)? ReparseForTemplate(
            InstantiateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup,
            CliTemplateInfo template,
            bool validateDefaultLanguage = false)
        {
            try
            {
                TemplateCommand command = new(
                    args.Command,
                    environmentSettings,
                    templatePackageManager,
                    templateGroup,
                    template,
                    validateDefaultLanguage);

                Parser parser = ParserFactory.CreateParser(command);
                ParseResult parseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());
                return (command, parseResult);
            }
            catch (InvalidTemplateParametersException e)
            {
                Reporter.Error.WriteLine(LocalizableStrings.GenericWarning, e.Message);
                return null;
            }
        }

        private static void SuggestTypoCorrections(InstantiateCommandArgs instantiateArgs, IEnumerable<TemplateGroup> templateGroups, IReporter reporter)
        {
            if (string.IsNullOrWhiteSpace(instantiateArgs.ShortName))
            {
                return;
            }

            IEnumerable<string> possibleTemplates = templateGroups
                .SelectMany(g => g.ShortNames);

            bool useInstantiateCommand = instantiateArgs.Command is InstantiateCommand;
            bool helpOption = instantiateArgs.HasHelpOption;
            IEnumerable<string> possibleTemplateMatches = TypoCorrection.GetSimilarTokens(possibleTemplates, instantiateArgs.ShortName);

            if (possibleTemplateMatches.Any())
            {
                reporter.WriteLine(LocalizableStrings.InstantiateCommand_Info_TypoCorrection_Templates);
                foreach (string possibleMatch in possibleTemplateMatches)
                {
                    Example example = useInstantiateCommand
                        ? Example.For<InstantiateCommand>(instantiateArgs.ParseResult).WithArgument(InstantiateCommand.ShortNameArgument, possibleMatch)
                        : Example.For<NewCommand>(instantiateArgs.ParseResult).WithArgument(NewCommand.ShortNameArgument, possibleMatch);
                    if (helpOption)
                    {
                        example = example.WithHelpOption();
                    }
                    reporter.WriteCommand(example);
                }
            }

            if (useInstantiateCommand)
            {
                //no subcommands in dotnet new create
                return;
            }

            IEnumerable<string> possibleSubcommands =
                instantiateArgs.Command.Subcommands
                    .Where(sc => !sc.IsHidden)
                    .SelectMany(sc => sc.Aliases);

            IEnumerable<string> possibleSubcommandsMatches = TypoCorrection.GetSimilarTokens(possibleSubcommands, instantiateArgs.ShortName);
            if (possibleSubcommandsMatches.Any())
            {
                reporter.WriteLine(LocalizableStrings.InstantiateCommand_Info_TypoCorrection_Subcommands);
                foreach (string possibleMatch in possibleSubcommandsMatches)
                {
                    Example example = Example.For<NewCommand>(instantiateArgs.ParseResult).WithSubcommand(possibleMatch);
                    if (helpOption)
                    {
                        example = example.WithHelpOption();
                    }
                    reporter.WriteCommand(example);
                }
            }
        }

        private static void WriteListCommandExample(InstantiateCommandArgs instantiateArgs, IReporter reporter)
        {
            if (!string.IsNullOrWhiteSpace(instantiateArgs.ShortName))
            {
                reporter.WriteLine(LocalizableStrings.Generic_CommandHints_List_Template, instantiateArgs.ShortName);
                reporter.WriteCommand(
                  Example
                      .For<NewCommand>(instantiateArgs.ParseResult)
                      .WithSubcommand<ListCommand>()
                      .WithArgument(ListCommand.NameArgument, instantiateArgs.ShortName));
            }
            else
            {
                reporter.WriteLine(LocalizableStrings.Generic_CommandHints_List);
                reporter.WriteCommand(
                    Example
                        .For<NewCommand>(instantiateArgs.ParseResult)
                        .WithSubcommand<ListCommand>());
            }
        }

        private static void WriteSearchCommandExample(InstantiateCommandArgs instantiateArgs, IReporter reporter)
        {
            if (!string.IsNullOrWhiteSpace(instantiateArgs.ShortName))
            {
                reporter.WriteLine(LocalizableStrings.Generic_CommandHints_Search);
                reporter.WriteCommand(
                    Example
                        .For<NewCommand>(instantiateArgs.ParseResult)
                        .WithSubcommand<SearchCommand>()
                        .WithArgument(SearchCommand.NameArgument, instantiateArgs.ShortName));
            }
        }
    }
}
