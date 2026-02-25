// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Uninstall;

internal static class SdkUninstallCommandParser
{
    public static readonly Argument<string> ChannelArgument = new("channel")
    {
        HelpName = "CHANNEL",
        Description = "The channel or version of the install spec to remove (e.g., 10, 9.0, 9.0.103).",
    };

    public static readonly Option<string> ManifestPathOption = new("--manifest-path")
    {
        HelpName = "MANIFEST_PATH",
        Description = "Custom path to the manifest file for tracking .NET SDK installations",
    };

    public static readonly Option<string> InstallPathOption = new("--install-path")
    {
        HelpName = "INSTALL_PATH",
        Description = "The dotnet root to uninstall from",
    };

    public static readonly Option<bool> NoProgressOption = CommonOptions.NoProgressOption;

    private static readonly Command SdkUninstallCommand = ConstructCommand();

    public static Command GetSdkUninstallCommand()
    {
        return SdkUninstallCommand;
    }

    private static readonly Command RootUninstallCommand = ConstructCommand();

    public static Command GetRootUninstallCommand()
    {
        return RootUninstallCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("uninstall", "Removes an install spec and cleans up unused installations");

        command.Arguments.Add(ChannelArgument);
        command.Options.Add(ManifestPathOption);
        command.Options.Add(InstallPathOption);
        command.Options.Add(NoProgressOption);

        command.SetAction(parseResult => new SdkUninstallCommand(parseResult).Execute());

        return command;
    }
}
