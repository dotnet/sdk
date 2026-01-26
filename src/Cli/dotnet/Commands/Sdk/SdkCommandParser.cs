// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Sdk.Check;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Sdk;

internal static class SdkCommandParser
{
    private static readonly SdkCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static SdkCommandDefinition CreateCommand()
    {
        var command = new SdkCommandDefinition();
        command.SetAction(parseResult => parseResult.HandleMissingCommand());
        command.CheckCommand.SetAction(SdkCheckCommand.Run);
        return command;
    }
}
