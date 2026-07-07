// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Workload.List;

internal static class WorkloadListCommandParser
{
    // arguments are a list of workload to be detected
    public static readonly Option<bool> MachineReadableOption = new("--machine-readable") { Hidden = true };

    public static readonly Option<string> VersionOption = InstallingWorkloadCommandParser.VersionOption;

    public static readonly Option<string> TempDirOption = new Option<string>("--temp-dir")
    {
        Description = CliCommandStrings.TempDirOptionDescription
    }.Hide();

    public static readonly Option<bool> IncludePreviewsOption = new Option<bool>("--include-previews")
    {
        Description = CliCommandStrings.IncludePreviewOptionDescription
    }.Hide();

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("list", CliCommandStrings.WorkloadListCommandDescription);
        command.Options.Add(MachineReadableOption);
        command.Options.Add(CommonOptions.HiddenVerbosityOption);
        command.Options.Add(VersionOption);
        command.Options.Add(TempDirOption);
        command.Options.Add(IncludePreviewsOption);
        command.AddWorkloadCommandNuGetRestoreActionConfigOptions(true);

        command.SetAction((parseResult) => new WorkloadListCommand(parseResult).Execute());

        return command;
    }
}
