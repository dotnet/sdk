// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.History;

internal static class WorkloadHistoryCommandDefinition
{
    public static Command Create()
    {
        var command = new Command("history", CliCommandStrings.WorkloadHistoryCommandDescription);

        return command;
    }
}
