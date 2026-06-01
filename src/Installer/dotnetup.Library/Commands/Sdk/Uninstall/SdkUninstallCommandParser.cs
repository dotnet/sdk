// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Uninstall;

internal static class SdkUninstallCommandParser
{
    // Each command needs its own Argument instance — see SdkInstallCommandParser
    // for the full explanation of why sharing causes silent parse failures.
    public static readonly Argument<string?> SdkChannelArgument =
        CommonOptions.CreateSdkChannelArgument(required: true, actionVerb: "remove");

    public static readonly Argument<string?> RootChannelArgument =
        CommonOptions.CreateSdkChannelArgument(required: true, actionVerb: "remove");

    private static readonly Command s_sdkUninstallCommand = ConstructCommand(SdkChannelArgument);

    public static Command GetSdkUninstallCommand()
    {
        return s_sdkUninstallCommand;
    }

    private static readonly Command s_rootUninstallCommand = ConstructCommand(RootChannelArgument);

    public static Command GetRootUninstallCommand()
    {
        return s_rootUninstallCommand;
    }

    private static Command ConstructCommand(Argument<string?> channelArgument)
    {
        Command command = new("uninstall", "Removes an install spec and cleans up unused installations");

        command.Arguments.Add(channelArgument);
        command.Options.Add(CommonOptions.SourceOption);
        command.Options.Add(CommonOptions.ManifestPathOption);
        command.Options.Add(CommonOptions.InstallPathOption);

        command.SetAction(parseResult => new SdkUninstallCommand(parseResult, channelArgument).Execute());

        return command;
    }
}
