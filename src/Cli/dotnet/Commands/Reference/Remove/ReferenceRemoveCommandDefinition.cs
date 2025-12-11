// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Reference.Remove;

internal static class ReferenceRemoveCommandDefinition
{
    public const string Name = "remove";
    public static readonly string ProjectPathArgument = CliCommandStrings.ReferenceRemoveProjectPathArgumentName;

    public static Argument<IEnumerable<string>> CreateProjectPathArgument() => new(ProjectPathArgument)
    {
        Description = CliCommandStrings.ReferenceRemoveProjectPathArgumentDescription,
        Arity = ArgumentArity.OneOrMore,
    };

    public static readonly Option<string> FrameworkOption = new("--framework", "-f")
    {
        Description = CliCommandStrings.ReferenceRemoveCmdFrameworkDescription,
        HelpName = CliStrings.CommonCmdFramework
    };

    public static Command Create()
    {
        var command = new Command(Name, CliCommandStrings.ReferenceRemoveAppFullName);

        command.Arguments.Add(CreateProjectPathArgument());
        command.Options.Add(FrameworkOption);

        return command;
    }
}
