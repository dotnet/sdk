// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

internal static class SdkInstallCommandParser
{
    public static readonly Argument<string?> ChannelArgument = new("channel")
    {
        HelpName = "CHANNEL",
        Description = "The channel of the .NET SDK to install.  For example: latest, 10, or 9.0.3xx.  A specific version (for example 9.0.304) can also be specified.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static readonly Option<string> InstallPathOption = CommonOptions.InstallPathOption;
    public static readonly Option<bool?> SetDefaultInstallOption = CommonOptions.SetDefaultInstallOption;
    public static readonly Option<string> ManifestPathOption = CommonOptions.ManifestPathOption;
    public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption;
    public static readonly Option<bool> NoProgressOption = CommonOptions.NoProgressOption;

    public static readonly Option<bool?> UpdateGlobalJsonOption = new("--update-global-json")
    {
        Description = "Update the sdk version in applicable global.json files to the installed SDK version",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = r => null
    };

    private static readonly Command SdkInstallCommand = ConstructCommand();

    public static Command GetSdkInstallCommand()
    {
        return SdkInstallCommand;
    }

    //  Trying to use the same command object for both "dotnetup install" and "dotnetup sdk install" causes the following exception:
    //  System.InvalidOperationException: Command install has more than one child named "channel".
    //  So we create a separate instance for each case
    private static readonly Command RootInstallCommand = ConstructCommand();

    public static Command GetRootInstallCommand()
    {
        return RootInstallCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("install", "Installs the .NET SDK");

        command.Arguments.Add(ChannelArgument);

        command.Options.Add(InstallPathOption);
        command.Options.Add(SetDefaultInstallOption);
        command.Options.Add(UpdateGlobalJsonOption);
        command.Options.Add(ManifestPathOption);

        command.Options.Add(InteractiveOption);
        command.Options.Add(NoProgressOption);

        command.SetAction(parseResult => new SdkInstallCommand(parseResult).Execute());

        return command;
    }
}
