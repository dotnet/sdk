// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    // Class to hold messages for the questions in interactive mode while in development
    // TODO: check and transfer messages to some place for translation
    internal static class InteractiveModePrompts
    {
        public static string OpeningMessage(string programmingLanguage, string templateName, string acceptDefaultsKey)
        {
            return $"Creating a new {programmingLanguage.Green()} {templateName.Blue().Bold()} project. Press '{acceptDefaultsKey.Green()}' to skip defaults";
        }
    }

    internal partial class InstantiateCommand
    {
        // Tree or List depending on future implementation choices
        // I do not know how to use generic types T_T
        private List<UserQuery<string>> parametersTree = new List<UserQuery<string>> { };
        private List<string> parametersToNotAsk = new List<string>
        {
            "TargetFrameworkOverride",
            "langVersion",
            "skipRestore",
            "type"
        };

        public void SetQuestions(
        InstantiateCommandArgs knownArgs,
        TemplateCommand template)
        {
            // Assumption: no decisions tree for the prototype
            var missingParams =
                template.GetMissingArguments(knownArgs.RemainingArguments)
                    .Where(p => !parametersToNotAsk.Contains(p.Key));

            foreach (var parameter in missingParams)
            {
                var paramInfo = parameter.Value;
                var prompt = $"What should the {paramInfo.Name.Green()} be?";
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
                var cliArgument = paramInfo.LongNameOverrides.Concat(paramInfo.ShortNameOverrides).Where(s => !string.IsNullOrEmpty(s)).FirstOrDefault() ?? paramInfo.Name;
                parametersTree.Add(new UserQuery<string>(cliArgument, prompt));
            }
        }

        public IEnumerator<UserQuery<string>> Questions()
        {
            foreach (var query in parametersTree)
            {
                yield return query;
            }
        }

        internal static TemplateCommand? GetTemplate(
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
                    foreach (CliTemplateInfo template in templateGrouping)
                    {
                        // Make some additional check based on user input match (like template language)
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

    internal class UserQuery<T>
    {
        private string parameterName;
        private string parameterMessage;

        public UserQuery(string name, string message)
        {
            parameterName = name;
            parameterMessage = message;
        }

        public string GetQuery()
        {
            return parameterMessage;
        }

        public Type GetValueType()
        {
            return typeof(T);
        }

        public string GetParameterName()
        {
            return parameterName;
        }
    }
}
