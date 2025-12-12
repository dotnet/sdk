// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Elevate;

internal static class WorkloadElevateCommandDefinition
{
    public static Command Create()
    {
        Command command = new("elevate", CliCommandStrings.WorkloadElevateCommandDescription)
        {
            Hidden = true
        };

        return command;
    }
}
