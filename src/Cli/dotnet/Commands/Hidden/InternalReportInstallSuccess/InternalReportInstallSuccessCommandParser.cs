// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Hidden.InternalReportInstallSuccess;

internal static class InternalReportInstallSuccessCommandParser
{
    private static readonly InternalReportInstallSuccessCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static InternalReportInstallSuccessCommandDefinition CreateCommand()
    {
        var command = new InternalReportInstallSuccessCommandDefinition();
        command.SetAction(InternalReportInstallSuccessCommand.Run);
        return command;
    }
}
