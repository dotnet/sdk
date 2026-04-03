// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Update;

internal static class RuntimeUpdateCommandParser
{
    private static readonly Command s_runtimeUpdateCommand = ConstructCommand();

    public static Command GetRuntimeUpdateCommand()
    {
        return s_runtimeUpdateCommand;
    }

    private static readonly Command s_rootUpdateCommand = ConstructCommand();

    public static Command GetRootUpdateCommand()
    {
        return s_rootUpdateCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("update", "Updates tracked .NET Runtime installations.");

        command.Options.Add(CommonOptions.ManifestPathOption);
        command.Options.Add(CommonOptions.InstallPathOption);
        command.Options.Add(CommonOptions.NoProgressOption);

        command.SetAction(parseResult => new RuntimeUpdateCommand(parseResult).Execute());

        return command;
    }
}
