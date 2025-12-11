// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Config;

internal static class WorkloadConfigCommandParser
{
    //  dotnet workload config --update-mode workload-set

    public static readonly string UpdateMode_WorkloadSet = WorkloadConfigCommandDefinition.UpdateMode_WorkloadSet;
    public static readonly string UpdateMode_Manifests = WorkloadConfigCommandDefinition.UpdateMode_Manifests;

    public static readonly Option<string> UpdateMode = WorkloadConfigCommandDefinition.UpdateMode;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = WorkloadConfigCommandDefinition.Create();

        command.SetAction(parseResult =>
        {
            new WorkloadConfigCommand(parseResult).Execute();
        });

        return command;
    }
}
