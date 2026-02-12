// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Format;

internal static class FormatCommandParser
{
    public static void ConfigureCommand(FormatCommandDefinition command)
    {
        command.SetAction(parseResult => FormatCommand.Run(parseResult.GetValue(command.Arguments) ?? []));
    }
}
