// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference.List;

namespace Microsoft.DotNet.Cli.Commands.Hidden.List.Reference;

internal static class ListReferenceCommandDefinition
{
    public const string Name = "reference";

    public static readonly Argument<string> Argument = new("argument") { Arity = ArgumentArity.ZeroOrOne, Hidden = true };

    public static Command Create()
    {
        var command = new Command(Name, CliCommandStrings.ReferenceListAppFullName);

        command.Arguments.Add(Argument);

        return command;
    }
}
