// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Reference.Add;

internal static class ReferenceAddCommandParser
{
    public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new(CliCommandStrings.ReferenceAddProjectPathArgumentName)
    {
        Description = CliCommandStrings.ReferenceAddProjectPathArgumentDescription,
        Arity = ArgumentArity.OneOrMore,
        CustomParser = arguments =>
        {
            var result = arguments.Tokens.TakeWhile(t => !t.Value.StartsWith("-"));
            arguments.OnlyTake(result.Count());
            return result.Select(t => t.Value);
        }
    };

    public static readonly Option<string> FrameworkOption = new DynamicOption<string>("--framework", "-f")
    {
        Description = CliCommandStrings.ReferenceAddCmdFrameworkDescription,
        HelpName = CliStrings.CommonCmdFramework

    }.AddCompletions(CliCompletion.TargetFrameworksFromProjectFile);

    public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption();

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("add", CliCommandStrings.ReferenceAddAppFullName);

        command.Arguments.Add(ProjectPathArgument);
        command.Options.Add(FrameworkOption);
        command.Options.Add(InteractiveOption);

        command.SetAction((parseResult) => new ReferenceAddCommand(parseResult).Execute());

        return command;
    }
}
