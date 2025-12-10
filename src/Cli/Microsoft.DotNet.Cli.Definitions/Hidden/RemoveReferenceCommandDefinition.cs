// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference.Remove;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Remove.Reference;

internal static class RemoveReferenceCommandDefinition
{
    public const string Name = "reference";

    public static Command Create()
    {
        var command = new Command(Name, CliCommandStrings.ReferenceRemoveAppFullName);

        command.Arguments.Add(ReferenceRemoveCommandDefinition.ProjectPathArgument);
        command.Options.Add(ReferenceRemoveCommandDefinition.FrameworkOption);

        return command;
    }
}
