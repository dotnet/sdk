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
        command.Options.Add(CommonOptions.LocalInstallOption);
        command.Options.Add(CommonOptions.SetDefaultInstallOption);
        command.Options.Add(CommonOptions.MigrateFromSystemOption);
        command.Options.Add(UpdateGlobalJsonOption);
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
        command.Validators.Add(RejectConflictingLocalInstallOptions());

        command.SetAction(parseResult => new SdkInstallCommand(parseResult).Execute());

        return command;
    }

    private static Action<System.CommandLine.Parsing.CommandResult> RejectConflictingLocalInstallOptions()
    {
        return commandResult =>
        {
            if (!HasOption(commandResult, CommonOptions.LocalInstallOption))
            {
                return;
            }

            RejectIfPresent(commandResult, CommonOptions.InstallPathOption, "The --local option chooses the project-local .dotnet install path, so it can't be combined with --install-path.");
            RejectIfPresent(commandResult, CommonOptions.SetDefaultInstallOption, "The --local option does not modify PATH or DOTNET_ROOT, so it can't be combined with --set-default-install.");
            RejectIfPresent(commandResult, CommonOptions.MigrateFromSystemOption, "The --local option installs only the requested project SDK, so it can't be combined with --migrate-from-system.");
            RejectIfPresent(commandResult, UpdateGlobalJsonOption, "The --local option always configures global.json, so it can't be combined with --update-global-json.");
        };
    }

    private static void RejectIfPresent(System.CommandLine.Parsing.CommandResult commandResult, Option option, string message)
    {
        if (HasOption(commandResult, option))
        {
            commandResult.AddError(message);
        }
    }

    private static bool HasOption(System.CommandLine.Parsing.CommandResult commandResult, Option option)
        => commandResult.GetResult(option) is not null;
}
