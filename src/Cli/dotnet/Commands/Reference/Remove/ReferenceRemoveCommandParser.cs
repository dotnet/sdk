// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Reference.Remove;

internal static class ReferenceRemoveCommandParser
{
    public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new DynamicArgument<IEnumerable<string>>(CliCommandStrings.ReferenceRemoveProjectPathArgumentName)
    {
        Description = CliCommandStrings.ReferenceRemoveProjectPathArgumentDescription,
        Arity = ArgumentArity.OneOrMore,
    }.AddCompletions(CliCompletion.ProjectReferencesFromProjectFile);

    public static readonly Option<string> FrameworkOption = new("--framework", "-f")
    {
        Description = CliCommandStrings.ReferenceRemoveCmdFrameworkDescription,
        HelpName = CliStrings.CommonCmdFramework
    };

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = new Command("remove", CliCommandStrings.ReferenceRemoveAppFullName);

        command.Arguments.Add(ProjectPathArgument);
        command.Options.Add(FrameworkOption);

        command.SetAction((parseResult) => new ReferenceRemoveCommand(parseResult).Execute());

        return command;
    }
}
