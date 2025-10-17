// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Update;

internal static class SdkUpdateCommandParser
{

    public static readonly Option<bool> UpdateAllOption = new("--all")
    {
        Description = "Update all installed SDKs",
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> UpdateGlobalJsonOption = new("--update-global-json")
    {
        Description = "Update the sdk version in applicable global.json files to the updated SDK version",
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption;
    public static readonly Option<bool> NoProgressOption = CommonOptions.NoProgressOption;

    private static readonly Command SdkUpdateCommand = ConstructCommand();

    public static Command GetSdkUpdateCommand()
    {
        return SdkUpdateCommand;
    }

    //  Trying to use the same command object for both "dnup udpate" and "dnup sdk update" causes an InvalidOperationException
    //  So we create a separate instance for each case
    private static readonly Command RootUpdateCommand = ConstructCommand();

    public static Command GetRootUpdateCommand()
    {
        return RootUpdateCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("update", "Updates the .NET SDK");

        command.Options.Add(UpdateAllOption);
        command.Options.Add(UpdateGlobalJsonOption);

        command.Options.Add(InteractiveOption);
        command.Options.Add(NoProgressOption);

        command.SetAction(parseResult => 0);

        return command;
    }
}
