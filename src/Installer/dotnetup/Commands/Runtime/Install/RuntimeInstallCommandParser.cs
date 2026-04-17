// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;

internal static class RuntimeInstallCommandParser
{
    public static readonly Argument<string[]> ComponentSpecsArgument =
        CommonOptions.CreateRuntimeComponentSpecsArgument(actionVerb: "install");

    private static readonly Command s_command = ConstructCommand();

    public static Command GetRuntimeInstallCommand()
    {
        return s_command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("install", "Installs a .NET Runtime");

        command.Arguments.Add(ComponentSpecsArgument);
        command.Options.Add(CommonOptions.InstallPathOption);
        command.Options.Add(CommonOptions.SetDefaultInstallOption);
        command.Options.Add(CommonOptions.ManifestPathOption);
        command.Options.Add(CommonOptions.InteractiveOption);
        // Intentionally do not expose --shell on install commands.
        // If a user wants to override shell detection for the profile-setup experience,
        // they can run `dotnetup init --shell <name>` before installing.
        command.Options.Add(CommonOptions.NoProgressOption);
        command.Options.Add(CommonOptions.VerbosityOption);
        command.Options.Add(CommonOptions.RequireMuxerUpdateOption);
        command.Options.Add(CommonOptions.UntrackedOption);
        command.Validators.Add(CommonOptions.RejectShellOptionOnInstallCommand());

        command.SetAction(parseResult => new RuntimeInstallCommand(parseResult).Execute());

        return command;
    }
}
