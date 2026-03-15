// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

internal static class SdkInstallCommandParser
{
    public static readonly Argument<string[]> ChannelArguments =
        CommonOptions.CreateSdkChannelArguments(actionVerb: "install");

    public static readonly Option<bool?> UpdateGlobalJsonOption = new("--update-global-json")
    {
        Description = "Update the sdk version in applicable global.json files to the installed SDK version",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = r => null
    };

    private static readonly Command s_sdkInstallCommand = ConstructCommand();

    public static Command GetSdkInstallCommand()
    {
        return s_sdkInstallCommand;
    }

    //  Trying to use the same command object for both "dotnetup install" and "dotnetup sdk install" causes the following exception:
    //  System.InvalidOperationException: Command install has more than one child named "channel".
    //  So we create a separate instance for each case
    private static readonly Command s_rootInstallCommand = ConstructCommand();

    public static Command GetRootInstallCommand()
    {
        return s_rootInstallCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("install", "Installs the .NET SDK");

        command.Arguments.Add(ChannelArguments);

        command.Options.Add(CommonOptions.InstallPathOption);
        command.Options.Add(CommonOptions.SetDefaultInstallOption);
        command.Options.Add(UpdateGlobalJsonOption);
        command.Options.Add(CommonOptions.ManifestPathOption);

        command.Options.Add(CommonOptions.InteractiveOption);
        command.Options.Add(CommonOptions.NoProgressOption);
        command.Options.Add(CommonOptions.RequireMuxerUpdateOption);
        command.Options.Add(CommonOptions.UntrackedOption);

        command.SetAction(parseResult => new SdkInstallCommand(parseResult).Execute());

        return command;
    }
}
