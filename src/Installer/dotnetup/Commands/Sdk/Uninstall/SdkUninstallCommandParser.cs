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

    internal static readonly Option<InstallSource> s_sourceOption = new("--source")
    {
        Description = "Filter by install source (explicit, globaljson, all).",
        DefaultValueFactory = _ => InstallSource.Explicit
    };

    public static Option<InstallSource> SourceOption => s_sourceOption;

    public static readonly Option<string> ManifestPathOption = CommonOptions.ManifestPathOption;

    public static readonly Option<string> InstallPathOption = CommonOptions.InstallPathOption;

    public static readonly Option<bool> NoProgressOption = CommonOptions.NoProgressOption;

    private static readonly Command s_sdkUninstallCommand = ConstructCommand();

    public static Command GetSdkUninstallCommand()
    {
        return s_sdkUninstallCommand;
    }

    private static readonly Command s_rootUninstallCommand = ConstructCommand();

    public static Command GetRootUninstallCommand()
    {
        return s_rootUninstallCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("uninstall", "Removes an install spec and cleans up unused installations");

        command.Arguments.Add(ChannelArgument);
        command.Options.Add(SourceOption);
        command.Options.Add(ManifestPathOption);
        command.Options.Add(InstallPathOption);
        command.Options.Add(NoProgressOption);

        command.SetAction(parseResult => new SdkUninstallCommand(parseResult).Execute());

        return command;
    }
}
