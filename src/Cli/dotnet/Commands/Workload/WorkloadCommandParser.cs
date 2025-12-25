// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.DotNet.Cli.Commands.Workload.Clean;
using Microsoft.DotNet.Cli.Commands.Workload.Config;
using Microsoft.DotNet.Cli.Commands.Workload.Elevate;
using Microsoft.DotNet.Cli.Commands.Workload.History;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Commands.Workload.List;
using Microsoft.DotNet.Cli.Commands.Workload.Repair;
using Microsoft.DotNet.Cli.Commands.Workload.Restore;
using Microsoft.DotNet.Cli.Commands.Workload.Search;
using Microsoft.DotNet.Cli.Commands.Workload.Uninstall;
using Microsoft.DotNet.Cli.Commands.Workload.Update;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.TemplateEngine.Cli.Commands;
using Command = System.CommandLine.Command;
using IReporter = Microsoft.DotNet.Cli.Utils.IReporter;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal static class WorkloadCommandParser
{
    private static readonly Command Command = ConfigureCommand(WorkloadCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConfigureCommand(Command command)
    {
        command.SetAction(parseResult => parseResult.HandleMissingCommand());
        return command;
    }
}
