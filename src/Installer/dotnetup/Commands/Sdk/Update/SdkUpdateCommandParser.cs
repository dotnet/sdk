// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Update;

internal static class SdkUpdateCommandParser
{

    public static readonly Option<bool> UpdateAllOption = new("--all")
    {
        Description = "Update all installed components, including runtimes and SDKs.",
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> UpdateGlobalJsonOption = new("--update-global-json")
    {
        Description = "Update the sdk version in applicable global.json files to the updated SDK version",
        Arity = ArgumentArity.Zero
    };

    private static readonly Command s_sdkUpdateCommand = ConstructCommand();

    public static Command GetSdkUpdateCommand()
    {
        return s_sdkUpdateCommand;
    }

    //  Trying to use the same command object for both "dotnetup update" and "dotnetup sdk update" causes an InvalidOperationException
    //  So we create a separate instance for each case
    private static readonly Command s_rootUpdateCommand = ConstructRootCommand();

    public static Command GetRootUpdateCommand()
    {
        return s_rootUpdateCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("update", "Updates the .NET SDK to the latest version matching each install spec");

        command.Options.Add(UpdateAllOption);
        command.Options.Add(UpdateGlobalJsonOption);
        command.Options.Add(CommonOptions.ManifestPathOption);
        command.Options.Add(CommonOptions.InstallPathOption);

        command.Options.Add(CommonOptions.InteractiveOption);
        command.Options.Add(CommonOptions.NoProgressOption);

        command.SetAction(parseResult => new SdkUpdateCommand(parseResult).Execute());

        return command;
    }

    private static Command ConstructRootCommand()
    {
        Command command = new("update", "Updates all tracked .NET installations to the latest versions");

        command.Options.Add(UpdateGlobalJsonOption);
        command.Options.Add(CommonOptions.ManifestPathOption);
        command.Options.Add(CommonOptions.InstallPathOption);

        command.Options.Add(CommonOptions.InteractiveOption);
        command.Options.Add(CommonOptions.NoProgressOption);

        command.SetAction(parseResult => new SdkUpdateCommand(parseResult, updateAllOverride: true).Execute());

        return command;
    }
}
