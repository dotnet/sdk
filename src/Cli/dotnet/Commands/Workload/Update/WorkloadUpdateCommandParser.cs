// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Workload.Install;

namespace Microsoft.DotNet.Cli.Commands.Workload.Update;

internal static class WorkloadUpdateCommandParser
{
    public static readonly Option<string> TempDirOption = WorkloadUpdateCommandDefinition.TempDirOption;

    public static readonly Option<bool> FromPreviousSdkOption = WorkloadUpdateCommandDefinition.FromPreviousSdkOption;

    public static readonly Option<bool> AdManifestOnlyOption = WorkloadUpdateCommandDefinition.AdManifestOnlyOption;

    public static readonly Option<bool> PrintRollbackOption = WorkloadUpdateCommandDefinition.PrintRollbackOption;

    public static readonly Option<int> FromHistoryOption = WorkloadUpdateCommandDefinition.FromHistoryOption;

    public static readonly Option<string> HistoryManifestOnlyOption = WorkloadUpdateCommandDefinition.HistoryManifestOnlyOption;

    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = WorkloadUpdateCommandDefinition.VerbosityOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = WorkloadUpdateCommandDefinition.Create();

        command.SetAction((parseResult) => new WorkloadUpdateCommand(parseResult).Execute());

        return command;
    }
}
