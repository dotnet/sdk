// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands.InteractiveMode
{
    public static class InteractiveMode
    {
        public static async Task EnterInteractiveMode(InvocationContext context, Func<InvocationContext, Task> next)
        {
            if (context.ParseResult.CommandResult.Command.Name == "new")
            {
                var parsedArgs = context.ParseResult.CommandResult.Children;
                var enterInteractiveMode = ShouldEnterInteractiveMode(parsedArgs);
                if (enterInteractiveMode)
                {
                    // TODO: add the default skip key
                    context.Console.WriteLine("Currently under development, not all features implemented yet");
                    // TODO: short name validation. right now we are assuming that the provided template exists and is valid
                    context.Console.WriteLine("Creating a new <language> <template name> project. Press 's' to skip defaults");

                    // Check the parser for arguments that we already have
                    var nameOption = parsedArgs.OfType<OptionResult>().Where(option => option.Option.Name == "name");
                    string? name;
                    if (!nameOption.Any())
                    {
                        context.Console.Write($"Choose a name for project: ");
                        name = Console.ReadLine();
                    }
                    name = (string?)nameOption.First().GetValueOrDefault();
                    // Ask missing basic template questions
                    // name
                    // output path

                }
            }
            await next(context).ConfigureAwait(false);
        }

        // True if we should ask user input interactively
        // False if we should just continue with the pipeline
        public static bool ShouldEnterInteractiveMode(IReadOnlyList<SymbolResult> parsedArgs)
        {
            bool interactiveOptionValue;
            var interactiveOption = parsedArgs.OfType<OptionResult>().Where(option => option.Option.Name == "interactive");
            if (!interactiveOption.Any())
            {
                return false;
            }

            var inter = interactiveOption.First().GetValueOrDefault();
            if (inter is null)
            {
                interactiveOptionValue = false;
            }
            else
            {
                interactiveOptionValue = true;
            }

            // Hard coded for now
            // bool envVariable = Environment.GetEnvironmentVariable("DOTNETNEWINTERACTIVE");
            bool envVriable = true;

            // hard coded for now
            // Check if current environment supports what we want to do (If we need any fancy stuff)
            bool supportsInteractiveMode = true;
            if ((interactiveOptionValue || envVriable) && supportsInteractiveMode)
            {
                return true;
            }

            return false;
        }
    }
}
