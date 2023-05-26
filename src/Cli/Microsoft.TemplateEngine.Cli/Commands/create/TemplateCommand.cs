﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;
using Command = System.CommandLine.Command;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class TemplateCommand : Command
    {
        private static readonly TimeSpan ConstraintEvaluationTimeout = TimeSpan.FromMilliseconds(1000);
        private static readonly string[] _helpAliases = new[] { "-h", "/h", "--help", "-?", "/?" };
        private readonly TemplatePackageManager _templatePackageManager;
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly BaseCommand _instantiateCommand;
        private readonly TemplateGroup _templateGroup;
        private readonly CliTemplateInfo _template;
        private Dictionary<string, TemplateOption> _templateSpecificOptions = new Dictionary<string, TemplateOption>();

        /// <summary>
        /// Create command for instantiation of specific template.
        /// </summary>
        /// <exception cref="InvalidTemplateParametersException">when <paramref name="template"/> has invalid template parameters.</exception>
        public TemplateCommand(
            BaseCommand instantiateCommand,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup,
            CliTemplateInfo template,
            bool buildDefaultLanguageValidation = false)
            : base(
                  templateGroup.ShortNames[0],
                  template.Name + Environment.NewLine + template.Description)
        {
            _instantiateCommand = instantiateCommand;
            _environmentSettings = environmentSettings;
            _templatePackageManager = templatePackageManager;
            _templateGroup = templateGroup;
            _template = template;
            foreach (var item in templateGroup.ShortNames.Skip(1))
            {
                AddAlias(item);
            }

            this.AddOption(SharedOptions.OutputOption);
            this.AddOption(SharedOptions.NameOption);
            this.AddOption(SharedOptions.DryRunOption);
            this.AddOption(SharedOptions.ForceOption);
            this.AddOption(SharedOptions.NoUpdateCheckOption);

            string? templateLanguage = template.GetLanguage();
            string? defaultLanguage = environmentSettings.GetDefaultLanguage();
            if (!string.IsNullOrWhiteSpace(templateLanguage))
            {
                LanguageOption = SharedOptionsFactory.CreateLanguageOption();
                LanguageOption.Description = SymbolStrings.TemplateCommand_Option_Language;
                LanguageOption.FromAmongCaseInsensitive(new[] { templateLanguage });

                if (!string.IsNullOrWhiteSpace(defaultLanguage)
                     && buildDefaultLanguageValidation)
                {
                    LanguageOption.SetDefaultValue(defaultLanguage);
                    LanguageOption.AddValidator(optionResult =>
                    {
                        var value = optionResult.GetValueOrDefault<string>();
                        if (value != template.GetLanguage())
                        {
                            optionResult.ErrorMessage = "Languages don't match";
                        }
                    }
                    );
                }
                this.AddOption(LanguageOption);
            }

            string? templateType = template.GetTemplateType();

            if (!string.IsNullOrWhiteSpace(templateType))
            {
                TypeOption = SharedOptionsFactory.CreateTypeOption();
                TypeOption.Description = SymbolStrings.TemplateCommand_Option_Type;
                TypeOption.FromAmongCaseInsensitive(new[] { templateType });
                this.AddOption(TypeOption);
            }

            if (template.BaselineInfo.Any(b => !string.IsNullOrWhiteSpace(b.Key)))
            {
                BaselineOption = SharedOptionsFactory.CreateBaselineOption();
                BaselineOption.Description = SymbolStrings.TemplateCommand_Option_Baseline;
                BaselineOption.FromAmongCaseInsensitive(template.BaselineInfo.Select(b => b.Key).Where(b => !string.IsNullOrWhiteSpace(b)).ToArray());
                this.AddOption(BaselineOption);
            }

            if (HasRunScriptPostActionDefined(template))
            {
                AllowScriptsOption = new Option<AllowRunScripts>("--allow-scripts")
                {
                    Description = SymbolStrings.TemplateCommand_Option_AllowScripts,
                    Arity = new ArgumentArity(1, 1)
                };
                AllowScriptsOption.SetDefaultValue(AllowRunScripts.Prompt);
                this.AddOption(AllowScriptsOption);
            }

            AddTemplateOptionsToCommand(template);
        }

        internal static IReadOnlyList<string> KnownHelpAliases => _helpAliases;

        internal Option<AllowRunScripts>? AllowScriptsOption { get; }

        internal Option<string>? LanguageOption { get; }

        internal Option<string>? TypeOption { get; }

        internal Option<string>? BaselineOption { get; }

        internal IReadOnlyDictionary<string, TemplateOption> TemplateOptions => _templateSpecificOptions;

        internal CliTemplateInfo Template => _template;

        internal static async Task<IReadOnlyList<TemplateConstraintResult>> ValidateConstraintsAsync(TemplateConstraintManager constraintManager, ITemplateInfo template, CancellationToken cancellationToken)
        {
            if (!template.Constraints.Any())
            {
                return Array.Empty<TemplateConstraintResult>();
            }

            IReadOnlyList<(ITemplateInfo Template, IReadOnlyList<TemplateConstraintResult> Result)> result = await constraintManager.EvaluateConstraintsAsync(new[] { template }, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<TemplateConstraintResult> templateConstraints = result.Single().Result;

            if (templateConstraints.IsTemplateAllowed())
            {
                return Array.Empty<TemplateConstraintResult>();
            }
            return templateConstraints.Where(cr => cr.EvaluationStatus != TemplateConstraintResult.Status.Allowed).ToList();
        }

        internal async Task<NewCommandStatus> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
        {
            TemplateCommandArgs args = new TemplateCommandArgs(this, _instantiateCommand, parseResult);
            TemplateInvoker invoker = new TemplateInvoker(_environmentSettings, () => Console.ReadLine() ?? string.Empty);
            TemplatePackageCoordinator packageCoordinator = new TemplatePackageCoordinator(_environmentSettings, _templatePackageManager);
            TemplateConstraintManager constraintManager = new TemplateConstraintManager(_environmentSettings);
            TemplatePackageDisplay templatePackageDisplay = new TemplatePackageDisplay(Reporter.Output, Reporter.Error);

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(ConstraintEvaluationTimeout);

            Task<IReadOnlyList<TemplateConstraintResult>> constraintsEvaluation = ValidateConstraintsAsync(constraintManager, args.Template, args.IsForceFlagSpecified ? cancellationTokenSource.Token : cancellationToken);

            if (!args.IsForceFlagSpecified)
            {
                var constraintResults = await constraintsEvaluation.ConfigureAwait(false);
                if (constraintResults.Any())
                {
                    DisplayConstraintResults(constraintResults, args);
                    return NewCommandStatus.CreateFailed;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            Task<NewCommandStatus> instantiateTask = invoker.InvokeTemplateAsync(args, cancellationToken);
            Task<(string Id, string Version, string Provider)> builtInPackageCheck = packageCoordinator.ValidateBuiltInPackageAvailabilityAsync(args.Template, cancellationToken);
            Task<CheckUpdateResult?> checkForUpdateTask = packageCoordinator.CheckUpdateForTemplate(args, cancellationToken);

            Task[] tasksToWait = new Task[] { instantiateTask, builtInPackageCheck, checkForUpdateTask };

            await Task.WhenAll(tasksToWait).ConfigureAwait(false);
            Reporter.Output.WriteLine();

            cancellationToken.ThrowIfCancellationRequested();

            if (checkForUpdateTask.Result != null)
            {
                // print if there is update for the template package containing the template
                templatePackageDisplay.DisplayUpdateCheckResult(checkForUpdateTask.Result, args);
            }

            if (builtInPackageCheck.Result != default)
            {
                // print if there is same or newer built-in package
                templatePackageDisplay.DisplayBuiltInPackagesCheckResult(
                    builtInPackageCheck.Result.Id,
                    builtInPackageCheck.Result.Version,
                    builtInPackageCheck.Result.Provider,
                    args);
            }

            if (args.IsForceFlagSpecified)
            {
                // print warning about the constraints that were not met.
                try
                {
                    IReadOnlyList<TemplateConstraintResult> constraintResults = await constraintsEvaluation.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                    if (constraintResults.Any())
                    {
                        DisplayConstraintResults(constraintResults, args);
                    }
                }
                catch (TaskCanceledException)
                {
                    // do nothing
                }
            }

            return instantiateTask.Result;
        }

        private void DisplayConstraintResults(IReadOnlyList<TemplateConstraintResult> constraintResults, TemplateCommandArgs templateArgs)
        {
            var reporter = templateArgs.IsForceFlagSpecified ? Reporter.Output : Reporter.Error;

            if (templateArgs.IsForceFlagSpecified)
            {
                reporter.WriteLine(LocalizableStrings.TemplateCommand_DisplayConstraintResults_Warning, templateArgs.Template.Name);
            }
            else
            {
                reporter.WriteLine(LocalizableStrings.TemplateCommand_DisplayConstraintResults_Error, templateArgs.Template.Name);
            }

            foreach (var constraint in constraintResults.Where(cr => cr.EvaluationStatus != TemplateConstraintResult.Status.Allowed))
            {
                reporter.WriteLine(constraint.ToDisplayString().Indent());
            }
            reporter.WriteLine();

            if (!templateArgs.IsForceFlagSpecified)
            {
                reporter.WriteLine(LocalizableStrings.TemplateCommand_DisplayConstraintResults_Hint, SharedOptions.ForceOption.Aliases.First());
                reporter.WriteCommand(Example.FromExistingTokens(templateArgs.ParseResult).WithOption(SharedOptions.ForceOption));
            }
            else
            {
                reporter.WriteLine(LocalizableStrings.TemplateCommand_DisplayConstraintResults_Hint_TemplateNotUsable);
            }
        }

        private bool HasRunScriptPostActionDefined(CliTemplateInfo template)
        {
            return template.PostActions.Contains(ProcessStartPostActionProcessor.ActionProcessorId);
        }

        private HashSet<string> GetReservedAliases()
        {
            HashSet<string> reservedAliases = new HashSet<string>();
            foreach (string alias in this.Children.OfType<Option>().SelectMany(o => o.Aliases))
            {
                reservedAliases.Add(alias);
            }
            foreach (string alias in this.Children.OfType<Command>().SelectMany(o => o.Aliases))
            {
                reservedAliases.Add(alias);
            }
            //add options of parent? - this covers debug: options
            foreach (string alias in _instantiateCommand.Children.OfType<Option>().SelectMany(o => o.Aliases))
            {
                reservedAliases.Add(alias);
            }
            foreach (string alias in _instantiateCommand.Children.OfType<Command>().SelectMany(o => o.Aliases))
            {
                reservedAliases.Add(alias);
            }

            //add restricted aliases: language, type, baseline (they may be optional)
            foreach (string alias in new[] { SharedOptionsFactory.CreateLanguageOption(), SharedOptionsFactory.CreateTypeOption(), SharedOptionsFactory.CreateBaselineOption() }.SelectMany(o => o.Aliases))
            {
                reservedAliases.Add(alias);
            }

            foreach (string helpAlias in KnownHelpAliases)
            {
                reservedAliases.Add(helpAlias);
            }
            return reservedAliases;
        }

        private void AddTemplateOptionsToCommand(CliTemplateInfo templateInfo)
        {
            HashSet<string> initiallyTakenAliases = GetReservedAliases();

            var parametersWithAliasAssignments = AliasAssignmentCoordinator.AssignAliasesForParameter(templateInfo.CliParameters.Values, initiallyTakenAliases);
            if (parametersWithAliasAssignments.Any(p => p.Errors.Any()))
            {
                IReadOnlyDictionary<CliTemplateParameter, IReadOnlyList<string>> errors = parametersWithAliasAssignments
                    .Where(p => p.Errors.Any())
                    .ToDictionary(p => p.Parameter, p => p.Errors);
                throw new InvalidTemplateParametersException(templateInfo, errors);
            }

            foreach ((CliTemplateParameter parameter, IReadOnlySet<string> aliases, IReadOnlyList<string> _) in parametersWithAliasAssignments)
            {
                TemplateOption option = new TemplateOption(parameter, aliases);
                this.AddOption(option.Option);
                _templateSpecificOptions[parameter.Name] = option;
            }
        }
    }
}
