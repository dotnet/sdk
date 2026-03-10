// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Uninstall;

internal static class RuntimeUninstallCommandParser
{
    public static readonly Argument<string?> ComponentSpecArgument =
        CommonOptions.CreateRuntimeComponentSpecArgument(required: true, actionVerb: "uninstall");

    private static readonly Command s_runtimeUninstallCommand = ConstructCommand();

    public static Command GetRuntimeUninstallCommand()
    {
        return s_runtimeUninstallCommand;
    }

    private static readonly Command s_rootUninstallCommand = ConstructCommand();

    public static Command GetRootUninstallCommand()
    {
        return s_rootUninstallCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("uninstall", "Removes a .NET Runtime Installation.");

        command.Arguments.Add(ComponentSpecArgument);
        command.Options.Add(CommonOptions.SourceOption);
        command.Options.Add(CommonOptions.ManifestPathOption);
        command.Options.Add(CommonOptions.InstallPathOption);

        command.SetAction(parseResult => new RuntimeUninstallCommand(parseResult).Execute());

        return command;
    }
}
