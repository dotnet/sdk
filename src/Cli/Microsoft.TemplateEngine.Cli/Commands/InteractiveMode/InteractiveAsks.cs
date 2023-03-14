// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.TemplateEngine.Cli.Commands.InteractiveMode
{
    // Class to hold messages for the questions in interactive mode while in development
    // TODO: check and transfer messages to some place for translation
    internal static class InteractiveModePrompts
    {
        public static string OpeningMessage(string programmingLanguage, string templateName, string accetpDefaultsKey)
        {
            return $"Creating a new {programmingLanguage} {templateName} project. Press '{accetpDefaultsKey}' to skip defaults";
        }
    }

    internal class InteractiveQuerying
    {
        // Tree or List depending on future implementation choices
        // I do not know how to use generic types T_T
        private List<UserQuery<string>> parametersTree;
        private List<string> parametersToNotAsk = new List<string>
        {
            "TargetFrameworkOverride",
            "langVersion",
            "skipRestore"
        };

        public InteractiveQuerying()
        {
            parametersTree = new List<UserQuery<string>>();
        }

        public void SetQuestions(
            InstantiateCommandArgs knownArgs,
            TemplateCommand template)
        {
            // Assumption: no decisions tree for the prototype
            var missingParams = template.GetMissingArguments(knownArgs.RemainingArguments);

            foreach (var parameter in missingParams)
            {
                // Can this check be used, or will we need more details to consider this?
                if (parameter.Value.Type == "parameter" && !parametersToNotAsk.Contains(parameter.Key))
                {
                    parametersTree.Add(new UserQuery<string>(parameter.Value.Name, $"Please enter parameter {parameter.Value.Name}"));
                }
            }
        }

        public IEnumerator<UserQuery<string>> Questions()
        {
            foreach (var query in parametersTree)
            {
                yield return query;
            }
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
