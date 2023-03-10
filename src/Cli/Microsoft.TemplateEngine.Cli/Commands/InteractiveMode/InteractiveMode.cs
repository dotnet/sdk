// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands.InteractiveMode
{
    public static class InteractiveMode
    {
        public static async Task EnterInteractiveMode(InvocationContext context, Func<InvocationContext, Task> next)
        {
            if (context.ParseResult.CommandResult.Command.Name == "new")
            {
                NewCommand? templateCommand = context.ParseResult.CommandResult.Command as NewCommand;
                if (templateCommand is null)
                {
                    return;
                }

                var enterInteractiveMode = ShouldEnterInteractiveMode(context.ParseResult, templateCommand);
                if (enterInteractiveMode)
                {
                    // TODO: add the default skip key
                    // TODO: short name validation. right now we are assuming that the provided template exists and is valid
                    context.Console.WriteLine("Currently under development, not all features implemented yet");

                    InteractiveQuerying questionCollection = new InteractiveQuerying();
                    context.Console.WriteLine(questionCollection.OpeningMessage("<language>", "<template Name>", "s"));

                    // tracks if we received all information necessary to instantiate the template
                    bool commandComplete = false;
                    var questions = questionCollection.Questions();

                    while (questions.MoveNext())
                    {
                        var currentQuestion = questions.Current;
                        context.Console.WriteLine(questions.currentQuestion.GetQuery());
                    }

                    while (!commandComplete)
                    {
                        //context.Console.WriteLine(GetMissingArguments(missingParam));
                        var paramValue = Console.ReadLine();
                        // Try to cast value, and then check if valid. Maybe put in a loop in case value was not valid
                        // Add to the command line text to reparse later
                    }

                    // After everything is done reparse the command line input and execute the result
                }
            }
            await next(context).ConfigureAwait(false);
        }

        // True if we should ask user input interactively
        // False if we should just continue with the pipeline
        internal static bool ShouldEnterInteractiveMode(ParseResult parsedArgs, NewCommand command)
        {
            // TODO: find a better way to query for the option
            // TODO: Better handling of this option when executing
            bool interactiveOptionValue = false;
            if (NewCommand.InteractiveTemplateOption is not null)
            {
                interactiveOptionValue = parsedArgs.GetValue(NewCommand.InteractiveTemplateOption);
            }

            var envVariable = Environment.GetEnvironmentVariable("DOTNETNEWINTERACTIVE");
            bool envVariableValue = Convert.ToBoolean(envVariable);

            if ((interactiveOptionValue || envVariableValue) && DoesSupportInteractive())
            {
                return true;
            }

            return false;
        }

        internal static bool DoesSupportInteractive()
        {
            // hard coded for now
            // Check if current environment supports what we want to do (If we need any fancy stuff)
            return true;
        }

        internal static ITemplateInfo GetTemplate(
            ParseResult parseResult,
            TemplatePackageManager templatePackageManager)
        {
            // Why do we get the templates, and then the template groups?
            IReadOnlyList<ITemplateInfo> templates =
                Task.Run(async () => await templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false)).GetAwaiter().GetResult();

            // IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templates, hostSpecificDataLoader));
            // TODO: Make this work when instantiating more than one template at a time
            return templates.First(template => template.ShortNameList.Contains("something on parseResult"));
        }
    }
}
