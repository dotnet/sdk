// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Fsi;

internal static class FsiCommandParser
{
    public static void ConfigureCommand(FsiCommandDefinition command)
    {
        command.SetAction((parseResult, cancellationToken) =>
            FsiCommand.Run(parseResult.GetValue(command.Arguments) ?? [], cancellationToken));
    }
}
