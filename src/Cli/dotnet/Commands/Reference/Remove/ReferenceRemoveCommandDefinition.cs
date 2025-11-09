// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.StaticCompletions;

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Reference.Remove;

internal static class ReferenceRemoveCommandDefinition
{
    public const string Name = "remove";

    public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new Argument<IEnumerable<string>>(CliCommandStrings.ReferenceRemoveProjectPathArgumentName)
    {
        Description = CliCommandStrings.ReferenceRemoveProjectPathArgumentDescription,
        Arity = ArgumentArity.OneOrMore,
        IsDynamic = true,
    }.AddCompletions(CliCompletion.ProjectReferencesFromProjectFile);

    public static readonly Option<string> FrameworkOption = new("--framework", "-f")
    {
        Description = CliCommandStrings.ReferenceRemoveCmdFrameworkDescription,
        HelpName = CliStrings.CommonCmdFramework
    };

    public static Command Create()
    {
        var command = new Command(Name, CliCommandStrings.ReferenceRemoveAppFullName);

        command.Arguments.Add(ProjectPathArgument);
        command.Options.Add(FrameworkOption);

        return command;
    }
}
