// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Hidden.InternalReportInstallSuccess;

internal static class InternalReportInstallSuccessCommandDefinition
{
    public static readonly Argument<string> Argument = new("internal-reportinstallsuccess-arg");

    public static Command Create()
    {
        Command command = new("internal-reportinstallsuccess")
        {
            Hidden = true
        };

        command.Arguments.Add(Argument);

        return command;
    }
}
