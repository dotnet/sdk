// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Reference.Remove;

internal static class ReferenceRemoveCommandParser
{
    public static readonly CliArgument<IEnumerable<string>> ProjectPathArgument = new DynamicArgument<IEnumerable<string>>(CliCommandStrings.ReferenceRemoveProjectPathArgumentName)
    {
        Description = CliCommandStrings.ReferenceRemoveProjectPathArgumentDescription,
        Arity = ArgumentArity.OneOrMore,
    }.AddCompletions(CliCompletion.ProjectReferencesFromProjectFile);

    public static readonly CliOption<string> FrameworkOption = new("--framework", "-f")
    {
        Description = CliCommandStrings.ReferenceRemoveCmdFrameworkDescription,
        HelpName = CliStrings.CommonCmdFramework
    };

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        var command = new CliCommand("remove", CliCommandStrings.ReferenceRemoveAppFullName);

        command.Arguments.Add(ProjectPathArgument);
        command.Options.Add(FrameworkOption);

        command.SetAction((parseResult) => new ReferenceRemoveCommand(parseResult).Execute());

        return command;
    }
}
