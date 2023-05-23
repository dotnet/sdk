// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.Invocation;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class InstantiateCommand : IInteractiveMode
    {
        private static List<string> parametersToNotAsk = new()
        {
            "TargetFrameworkOverride",
            "langVersion",
            "skipRestore",
            "type"
        };

        public Task<Questionnaire> GetQuestionsAsync(InvocationContext context, CancellationToken cancellationToken)
        {
            InstantiateCommandArgs instantiateCommandArgs = new(this, context.ParseResult);
            using IEngineEnvironmentSettings environmentSettings = CreateEnvironmentSettings(instantiateCommandArgs, context.ParseResult);
            return GetQuestionsInternalAsync(context, instantiateCommandArgs, environmentSettings);
        }

        internal static async Task<Questionnaire> GetQuestionsInternalAsync(InvocationContext context, InstantiateCommandArgs instantiateCommandArgs, IEngineEnvironmentSettings environmentSettings)
        {
            TemplatePackageManager templatePackageManager = new(environmentSettings);
            HostSpecificDataLoader? hostSpecificDataLoader = new(environmentSettings);

            IReadOnlyList<ITemplateInfo> templates = await templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false);

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templates, hostSpecificDataLoader));

            TemplateCommand? templateCommand = GetTemplate(
            instantiateCommandArgs,
                environmentSettings,
                templateGroups,
                templatePackageManager);

            if (templateCommand is null)
            {
                return Questionnaire.Empty;
            }
            return GetQuestions(instantiateCommandArgs, templateCommand);
        }

        private static Questionnaire GetQuestions(InstantiateCommandArgs knownArgs, TemplateCommand template)
        {
            // Assumption: no decisions tree for the prototype
            IEnumerable<KeyValuePair<string, CliTemplateParameter>> missingParams =
                template.GetMissingArguments(knownArgs.RemainingArguments)
                    .Where(p => !parametersToNotAsk.Contains(p.Key));

            List<UserQuery> parametersTree = new();

            foreach (var parameter in missingParams)
            {
                var paramInfo = parameter.Value;
                var prompt = $"What should the {paramInfo.Name.Green()} be";
                if (paramInfo is ChoiceTemplateParameter choiceParam)
                {
                    prompt += " " + "[".Blue();
                    prompt += string.Join(",", choiceParam.Choices.Select(choice =>
                    {
                        var displayString = string.IsNullOrEmpty(choice.Value.DisplayName) ? choice.Key : choice.Value.DisplayName;
                        var isDefault = displayString == paramInfo.DefaultValue;
                        return isDefault ? displayString.Green() : displayString.Blue();
                    }
                    ));
                    prompt += "]".Blue();
                }
                else if (paramInfo.Type == ParameterType.Boolean)
                {
                    var boolChoices = new[] { "true", "false" };
                    prompt += " " + "[".Blue();
                    prompt += string.Join(",", boolChoices.Select(choice =>
                    {
                        var isDefault = choice == paramInfo.DefaultValue;
                        return isDefault ? choice.Green() : choice.Blue();
                    }
                    ));
                    prompt += "]".Blue();
                }
                else if (paramInfo.DefaultValue is { } defaultValue)
                {
                    prompt += " " + $"[{defaultValue}]".Blue();
                }
                prompt += "? ";
                var cliArgument = paramInfo.LongNameOverrides.Concat(paramInfo.ShortNameOverrides).Where(s => !string.IsNullOrEmpty(s)).FirstOrDefault() ?? paramInfo.Name;
                parametersTree.Add(new UserQuery(cliArgument, prompt));
            }
            return new Questionnaire()
            {
                OpeningMessage = $"Creating a new {template.Template.GetLanguage().Green()} {template.Template.Name.Blue().Bold()} project. Press 'skip button' to skip defaults",
                Questions = parametersTree
            };
        }

        private static TemplateCommand? GetTemplate(
            InstantiateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            IEnumerable<TemplateGroup> templateGroups,
            TemplatePackageManager templatePackageManager)
        {
            TemplateConstraintManager constraintManager = new(environmentSettings);

            foreach (TemplateGroup templateGroup in templateGroups.Where(template => template.ShortNames.Contains(args.ShortName)))
            {
                foreach (IGrouping<int, CliTemplateInfo> templateGrouping in GetAllowedTemplates(constraintManager, templateGroup).GroupBy(g => g.Precedence).OrderByDescending(g => g.Key))
                {
                    //TODO: this ordering is not needed after the proper resolution algorithm is in place.
                    //For now it is added to make results consistent.
                    foreach (CliTemplateInfo template in templateGrouping.OrderBy(t => t.Identity))
                    {
                        // Make some additional check based on user input match (like template language)
                        // TODO: this requires better resolution: match on language, type, parameters.
                        try
                        {
                            TemplateCommand command = new(
                                args.Command,
                                environmentSettings,
                                templatePackageManager,
                                templateGroup,
                                template);

                            // simplify this for now to just return one template
                            return command;
                        }
                        catch (InvalidTemplateParametersException e)
                        {
                            Console.Error.WriteLine(LocalizableStrings.GenericWarning, e.Message);
                        }
                    }
                }
            }
            return null;
        }

    }
}
