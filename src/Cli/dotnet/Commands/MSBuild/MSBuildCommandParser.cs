// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.MSBuild;

internal static class MSBuildCommandParser
{
    public static void ConfigureCommand(MSBuildCommandDefinition command)
    {
        command.SetAction(MSBuildCommand.Run);
    }
}
