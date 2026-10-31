// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.SelfSign;

internal static class SelfSignCommandParser
{
    public static void ConfigureCommand(SelfSignCommandDefinition command)
    {
        command.SetAction(parseResult => SelfSignCommand.Run(parseResult, command));
    }
}
