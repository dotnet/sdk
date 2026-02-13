// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference.Add;
using Microsoft.DotNet.Cli.Commands.Reference.List;
using Microsoft.DotNet.Cli.Commands.Reference.Remove;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Reference;

internal static class ReferenceCommandParser
{
    public static void ConfigureCommand(ReferenceCommandDefinition command)
    {
        command.SetAction(parseResult => parseResult.HandleMissingCommand());

        command.AddCommand.SetAction(parseResult => new ReferenceAddCommand(parseResult).Execute());
        command.AddCommand.FrameworkOption.AddCompletions(CliCompletion.TargetFrameworksFromProjectFile);

        command.ListCommand.SetAction(parseResult => new ReferenceListCommand(parseResult).Execute());

        var projectPathArgument = command.RemoveCommand.ProjectPathArgument;
        projectPathArgument.CompletionSources.Add(CliCompletion.ProjectReferencesFromProjectFile);
        projectPathArgument.IsDynamic = true;

        command.RemoveCommand.SetAction(parseResult => new ReferenceRemoveCommand(parseResult).Execute());
    }
}
