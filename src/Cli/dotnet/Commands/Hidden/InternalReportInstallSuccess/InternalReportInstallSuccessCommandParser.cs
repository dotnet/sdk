// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Hidden.InternalReportInstallSuccess;

internal static class InternalReportInstallSuccessCommandParser
{
    public static void ConfigureCommand(InternalReportInstallSuccessCommandDefinition command)
    {
        command.SetAction(InternalReportInstallSuccessCommand.Run);
    }
}
