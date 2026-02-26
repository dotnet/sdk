// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Fsi;

internal static class FsiCommandParser
{
    public static void ConfigureCommand(FsiCommandDefinition command)
    {
        command.SetAction(parseResult => FsiCommand.Run(parseResult.GetValue(command.Arguments) ?? []));
    }
}
