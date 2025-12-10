// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference;
using Microsoft.DotNet.Cli.Commands.Reference.Add;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add.Reference;

internal static class AddReferenceCommandDefinition
{
    public const string Name = "reference";

    public static Command Create()
    {
        Command command = new(Name, CliCommandStrings.ReferenceAddAppFullName);

        command.Arguments.Add(ReferenceAddCommandDefinition.ProjectPathArgument);
        command.Options.Add(ReferenceAddCommandDefinition.FrameworkOption);
        command.Options.Add(ReferenceAddCommandDefinition.InteractiveOption);
        command.Options.Add(ReferenceCommandDefinition.ProjectOption);

        return command;
    }
}
