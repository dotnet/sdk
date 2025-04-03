// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Sdk.Check.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Commands.Sdk.Check;

internal static class SdkCheckCommandParser
{
    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("check", LocalizableStrings.AppFullName);

        command.SetAction(SdkCheckCommand.Run);

        return command;
    }
}
