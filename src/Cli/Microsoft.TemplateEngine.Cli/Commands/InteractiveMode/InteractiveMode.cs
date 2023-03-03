// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;

namespace Microsoft.TemplateEngine.Cli.Commands.InteractiveMode
{
    public static class InteractiveMode
    {
        public static async Task EnterInteractiveMode(InvocationContext context, Func<InvocationContext, Task> next)
        {
            if (context.ParseResult.CommandResult.Command.Name == "new")
            {
                TemplateCommand? templateCommand = context.ParseResult.CommandResult.Command as TemplateCommand;
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
                    context.Console.WriteLine("Creating a new <language> <template name> project. Press 's' to skip defaults");

                    // tracks if we received all information necessary to instantiate the template
                    bool commandComplete = false;
                    while (!commandComplete)
                    {
                        var missingParam = templateCommand.GetMissingArguments();
                        context.Console.WriteLine(GetMissingArguments(missingParam));
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
        internal static bool ShouldEnterInteractiveMode(ParseResult parsedArgs, TemplateCommand command)
        {
            // TODO: find a better way to query for the option
            // TODO: Better handling of this option when executing
            bool interactiveOptionValue = false;
            if (command.InteractiveOption is not null)
            {
                interactiveOptionValue = parsedArgs.GetValue(command.InteractiveOption);
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
    }
}
