// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.TemplateEngine.Cli.Commands.InteractiveMode
{
    public static class InteractiveMode
    {
        internal static Option<bool> InteractiveOption { get; } = new("--interactive-mode")
        {
            // TODO: link the right description here
            Description = SymbolStrings.Option_Interactive,
            IsHidden = true
        };

        public static async Task EnterInteractiveMode(InvocationContext context, Func<InvocationContext, Task> next)
        {
            if (context.ParseResult.CommandResult.Command is not IInteractiveMode interactiveModeCommand)
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            CancellationToken cancellationToken = context.GetCancellationToken();
            bool enterInteractiveMode = ShouldEnterInteractiveMode(context.ParseResult);
            if (!enterInteractiveMode)
            {
                await next(context).ConfigureAwait(false);
                return;
            }
            Questionnaire questionsToAsk = await interactiveModeCommand.GetQuestionsAsync(context, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (!questionsToAsk.Questions.Any())
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(questionsToAsk.OpeningMessage))
            {
                Reporter.Output.Write(questionsToAsk.OpeningMessage);
            }

            // TODO: adjust to different input types
            Dictionary<string, string> paramsToAdd = new();
            foreach (UserQuery currentQuestion in questionsToAsk.Questions)
            {
                bool rightInfoCollected = false;
                while (!rightInfoCollected)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Reporter.Output.Write(currentQuestion.ParameterMessage);

                    string? paramValue = Console.ReadLine();
                    if (paramValue is not null
                        && paramValue != string.Empty)
                    // && paramValue.GetType() == currentQuestion.GetValueType())
                    {
                        // only supporting string for now
                        paramsToAdd.Add(currentQuestion.ParameterName, paramValue);
                        rightInfoCollected = true;
                        //TODO: Necessary checks to see if value by customer is good
                    }
                    if (paramValue == string.Empty)
                    {
                        rightInfoCollected = true;
                    }
                }
            }

            List<string> tokens = context.ParseResult.CommandResult.Tokens.Select(t => t.Value).ToList();
            tokens.RemoveAll(t => InteractiveOption.Name.Equals(t) || InteractiveOption.Aliases.Contains(t));

            string commandLineCall = string.Join(' ', tokens);
            foreach (KeyValuePair<string, string> param in paramsToAdd)
            {
                commandLineCall += ($" --{param.Key} {param.Value}");
            }

            // After everything is done reparse the command line input and execute the result
            Reporter.Output.WriteLine("New command to be executed: " + commandLineCall.Green());
            Parser parser = ParserFactory.CreateParser(context.ParseResult.RootCommandResult.Command);
            parser.Invoke(commandLineCall);
        }

        internal static bool ShouldEnterInteractiveMode(ParseResult parsedArgs)
        {
            bool interactiveOptionValue = parsedArgs.GetValue(InteractiveOption);
            bool envVariable = Env.GetEnvironmentVariableAsBool("DOTNET_NEW_INTERACTIVE_MODE");
            if ((interactiveOptionValue || envVariable) && DoesSupportInteractive())
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
