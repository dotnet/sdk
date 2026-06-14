// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Sdk.Check;
#if !CLI_AOT
using Microsoft.DotNet.Cli.Extensions;
#endif

namespace Microsoft.DotNet.Cli.Commands.Sdk;

internal static class SdkCommandParser
{
    public static void ConfigureCommand(SdkCommandDefinition command)
    {
#if CLI_AOT
        // Bare `dotnet sdk` (no subcommand) needs full help/usage, which requires the managed CLI.
        command.SetAction((Func<ParseResult, int>)(_ => throw new CommandNotAvailableInAotException()));
#else
        command.SetAction(parseResult => parseResult.HandleMissingCommand());
#endif
        command.CheckCommand.SetAction(SdkCheckCommand.Run);
    }
}
