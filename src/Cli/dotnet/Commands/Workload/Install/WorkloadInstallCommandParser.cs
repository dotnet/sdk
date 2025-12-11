// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

internal static class WorkloadInstallCommandParser
{
    public static readonly Argument<IEnumerable<string>> WorkloadIdArgument = WorkloadInstallCommandDefinition.WorkloadIdArgument;

    public static readonly Option<bool> SkipSignCheckOption = WorkloadInstallCommandDefinition.SkipSignCheckOption;

    public static readonly Option<bool> SkipManifestUpdateOption = WorkloadInstallCommandDefinition.SkipManifestUpdateOption;

    public static readonly Option<string> TempDirOption = WorkloadInstallCommandDefinition.TempDirOption;

    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = WorkloadInstallCommandDefinition.VerbosityOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = WorkloadInstallCommandDefinition.Create();

        command.SetAction((parseResult) => new WorkloadInstallCommand(parseResult).Execute());

        return command;
    }

    internal static void AddWorkloadInstallCommandOptions(Command command)
    {
        WorkloadInstallCommandDefinition.AddWorkloadInstallCommandOptions(command);
    }
}

