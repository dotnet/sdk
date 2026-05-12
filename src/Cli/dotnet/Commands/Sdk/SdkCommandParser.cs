// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Sdk.Check;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Sdk;

internal static class SdkCommandParser
{
    public static void ConfigureCommand(SdkCommandDefinition command)
    {
        command.SetAction(parseResult => parseResult.HandleMissingCommand());
        command.CheckCommand.SetAction(SdkCheckCommand.Run);
    }
}
