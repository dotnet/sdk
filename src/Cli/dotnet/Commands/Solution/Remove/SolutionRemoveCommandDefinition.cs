// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Solution.Remove;

public static class SolutionRemoveCommandDefinition
{
    public const string Name = "remove";

    public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new(CliCommandStrings.RemoveProjectPathArgumentName)
    {
        HelpName = CliCommandStrings.RemoveProjectPathArgumentName,
        Description = CliCommandStrings.RemoveProjectPathArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static Command Create()
    {
        Command command = new(Name, CliCommandStrings.RemoveAppFullName);
        command.Arguments.Add(ProjectPathArgument);
        return command;
    }
}
