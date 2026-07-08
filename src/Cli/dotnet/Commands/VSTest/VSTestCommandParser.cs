// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.VSTest;

internal static class VSTestCommandParser
{
    public static void ConfigureCommand(VSTestCommandDefinition command)
    {
        command.SetAction(VSTestCommand.Run);
    }
}
