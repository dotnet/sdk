// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands.InteractiveMode
{
    public static class InteractiveMode
    {
        public static async Task EnterInteractiveMode(InvocationContext context, Func<InvocationContext, Task> next)
        {
            // TODO: make sure we have access to Instantiate command
            if (context.ParseResult.CommandResult.Command.Name == "new" || context.ParseResult.CommandResult.Command.Name == "create")
            {
                InstantiateCommand? instantiateCommand = GetInstantiateCommand(context.ParseResult.CommandResult.Command);
                if (instantiateCommand is null)
                {
                    return;
                }

                var enterInteractiveMode = ShouldEnterInteractiveMode(context.ParseResult);
                if (enterInteractiveMode)
                {
                    // TODO: add the default skip key
                    // TODO: short name validation. right now we are assuming that the provided template exists and is valid
                    context.Console.WriteLine("Currently under development, not all features implemented yet");

                    // --- Workaround to get the environmental settings at this stage. Firgure this out later
                    ITemplateEngineHost host = instantiateCommand.Host(context.ParseResult);
                    IEnvironment environment = new CliEnvironment();
                    IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(
                        host,
                        environment: environment,
                        pathInfo: new CliPathInfo(host, environment, null));

                    TemplatePackageManager templatePackageManager = new(settings);
                    HostSpecificDataLoader? hostSpecificDataLoader = new(settings);

                    IReadOnlyList<ITemplateInfo> templates =
                        Task.Run(async () => await templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false)).GetAwaiter().GetResult();

                    IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templates, hostSpecificDataLoader));
                    // --- End of env workaround

                    var knownArgs = GetArgs(context.ParseResult);
                    if (knownArgs is null)
                    {
                        return;
                    }

                    InteractiveQuerying queries = new InteractiveQuerying();
                    TemplateCommand? templateCommand = GetTemplate(
                        knownArgs,
                        settings,
                        templateGroups,
                        templatePackageManager);

                    if (templateCommand is null)
                    {
                        return;
                    }

                    context.Console.WriteLine(InteractiveModePrompts.OpeningMessage("<Default Language>", templateCommand.Template.Name, "skip button"));

                    queries.SetQuestions(knownArgs, templateCommand);

                    var questionsToAsk = queries.Questions();
                    // TODO: adjust to different input types
                    Dictionary<string, string> paramsToAdd = new Dictionary<string, string>();

                    while (questionsToAsk.MoveNext())
                    {
                        var currentQuestion = questionsToAsk.Current;
                        bool rightInfoCollected = false;
                        while (!rightInfoCollected)
                        {
                            context.Console.WriteLine(currentQuestion.GetQuery());
                            var paramValue = Console.ReadLine();
                            if (paramValue is not null
                                && paramValue != string.Empty
                                && paramValue.GetType() == currentQuestion.GetValueType())
                            {
                                // only supporting string for now
                                paramsToAdd.Add(currentQuestion.GetParameterName(), paramValue);
                                rightInfoCollected = true;
                                //TODO: Necessary checks to see if value by customer is good
                            }
                            if (paramValue == string.Empty)
                            {
                                rightInfoCollected = true;
                            }
                        }
                    }

                    // Hard coded for now bc I cannot find where this information would be in the parser / parserResult
                    // Need to get input given by user
                    string commandLineCall = $"dotnet new {templateCommand.Template.ShortNameList[0]}";
                    foreach (var param in paramsToAdd)
                    {
                        commandLineCall += ($" --{param.Key} {param.Value}");
                    }
                    // After everything is done reparse the command line input and execute the result

                    Parser parser = ParserFactory.CreateParser(context.ParseResult.RootCommandResult.Command);
                    parser.Invoke(commandLineCall);
                }
            }
            else
            {
                await next(context).ConfigureAwait(false);
            }
        }

        internal static InstantiateCommand? GetInstantiateCommand(Command command)
        {
            NewCommand? newCommand = command as NewCommand;
            InstantiateCommand? instantiateCommand;
            if (newCommand is null)
            {
                instantiateCommand = command as InstantiateCommand;
            }
            else
            {
                instantiateCommand = newCommand.Subcommands.First(command => command.Name == "create") as InstantiateCommand;
            }

            return instantiateCommand;
        }

        internal static InstantiateCommandArgs? GetArgs(ParseResult parseResult)
        {
            NewCommand? newCommand = parseResult.CommandResult.Command as NewCommand;
            InstantiateCommandArgs? knownArgs = null;
            if (newCommand is null)
            {
                var instantiateCommand = parseResult.CommandResult.Command as InstantiateCommand;
                if (instantiateCommand is not null)
                {
                    knownArgs = new InstantiateCommandArgs(instantiateCommand, parseResult);
                }
            }
            else
            {
                var newCommandArgs = new NewCommandArgs(newCommand, parseResult);
                knownArgs = InstantiateCommandArgs.FromNewCommandArgs(newCommandArgs);
            }

            return knownArgs;
        }

        internal static bool ShouldEnterInteractiveMode(ParseResult parsedArgs)
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

        private static IEnumerable<CliTemplateInfo> GetAllowedTemplates(TemplateConstraintManager constraintManager, TemplateGroup templateGroup)
        {
            //if at least one template in the group has constraint, they must be evaluated
            if (templateGroup.Templates.SelectMany(t => t.Constraints).Any())
            {
                CancellationTokenSource cancellationTokenSource = new();
                Task<IEnumerable<CliTemplateInfo>> constraintEvaluationTask = templateGroup.GetAllowedTemplatesAsync(constraintManager, cancellationTokenSource.Token);
                Task.Run(async () =>
                {
                    try
                    {
                        await constraintEvaluationTask.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        //do nothing
                    }
                }).GetAwaiter().GetResult();

                if (constraintEvaluationTask.IsCompletedSuccessfully)
                {
                    //return only allowed templates
                    return constraintEvaluationTask.Result;
                }
                //if evaluation task fails, all the templates in a group are considered as allowed.
                //in case the template may not be run, it will fail during instantiation.
            }
            return templateGroup.Templates;
        }

    }
}
