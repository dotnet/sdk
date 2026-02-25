// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Update;

internal static class SdkUpdateCommandParser
{

    public static readonly Option<bool> UpdateAllOption = new("--all")
    {
        Description = "Update all installed components, not just SDKs",
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> UpdateGlobalJsonOption = new("--update-global-json")
    {
        Description = "Update the sdk version in applicable global.json files to the updated SDK version",
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<string> ManifestPathOption = new("--manifest-path")
    {
        HelpName = "MANIFEST_PATH",
        Description = "Custom path to the manifest file for tracking .NET SDK installations",
    };

    public static readonly Option<string> InstallPathOption = new("--install-path")
    {
        HelpName = "INSTALL_PATH",
        Description = "The dotnet root to update",
    };

    public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption;
    public static readonly Option<bool> NoProgressOption = CommonOptions.NoProgressOption;

    private static readonly Command s_sdkUpdateCommand = ConstructCommand();

    public static Command GetSdkUpdateCommand()
    {
        return s_sdkUpdateCommand;
    }

    //  Trying to use the same command object for both "dotnetup update" and "dotnetup sdk update" causes an InvalidOperationException
    //  So we create a separate instance for each case
    private static readonly Command s_rootUpdateCommand = ConstructCommand();

    public static Command GetRootUpdateCommand()
    {
        return s_rootUpdateCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("update", "Updates the .NET SDK to the latest version matching each install spec");

        command.Options.Add(UpdateAllOption);
        command.Options.Add(UpdateGlobalJsonOption);
        command.Options.Add(ManifestPathOption);
        command.Options.Add(InstallPathOption);

        command.Options.Add(InteractiveOption);
        command.Options.Add(NoProgressOption);

        command.SetAction(parseResult => new SdkUpdateCommand(parseResult).Execute());

        return command;
    }
}
