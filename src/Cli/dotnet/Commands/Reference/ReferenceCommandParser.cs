// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.Commands.Reference.Add;
using Microsoft.DotNet.Cli.Commands.Reference.List;
using Microsoft.DotNet.Cli.Commands.Reference.Remove;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Reference;

internal static class ReferenceCommandParser
{
    private static readonly Command Command = SetActionsAndCompletion(new ReferenceCommandDefinition());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command SetActionsAndCompletion(ReferenceCommandDefinition def)
    {
        def.SetAction(parseResult => parseResult.HandleMissingCommand());

        def.AddCommand.SetAction(parseResult => new ReferenceAddCommand(parseResult).Execute());
        def.ListCommand.SetAction(parseResult => new ReferenceListCommand(parseResult).Execute());

        var projectPathArgument = def.RemoveCommand.ProjectPathArgument;

        projectPathArgument.CompletionSources.Add(CliCompletion.ProjectReferencesFromProjectFile);
        projectPathArgument.IsDynamic = true;

        def.RemoveCommand.SetAction(parseResult => new ReferenceRemoveCommand(parseResult).Execute());

        return def;
    }
}
