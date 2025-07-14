// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.Cli.Commands.Sdk.Install;

internal static class SdkInstallCommandParser
{
    

    public static readonly DynamicArgument<string?> ChannelArgument = new("channel")
    {
        HelpName = "CHANNEL",
        Description = "The channel of the .NET SDK to install.  For example: latest, 10, or 9.0.3xx.  A specific version (for example 9.0.304) can also be specified.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static readonly Option<string> InstallPathOption = new("--install-path")
    {
        HelpName = "INSTALL_PATH",
        Description = "The path to install the .NET SDK to",
    };

    //  TODO: Ideally you could just specify --set-default-root, as well as --set-default-root true or --set-default-root false
    //  This would help for interactivity
    public static readonly Option<bool> SetDefaultRootOption = new("--set-default-root")
    {
        Description = "Add installation path to PATH and set DOTNET_ROOT",
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption();

    private static readonly Command SdkInstallCommand = ConstructCommand();

    public static Command GetSdkInstallCommand()
    {
        return SdkInstallCommand;
    }

    //  Trying to use the same command object for both "dotnet install" and "dotnet sdk install" causes the following exception:
    //  System.InvalidOperationException: Command install has more than one child named "versionOrChannel".
    //  So we create a separate instance for each case
    private static readonly Command RootInstallCommand = ConstructCommand();

    public static Command GetRootInstallCommand()
    {
        return RootInstallCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("install", "Installs the .NET SDK");

        command.Arguments.Add(VersionOrChannelArgument);

        command.Options.Add(InstallPathOption);
        command.Options.Add(SetDefaultRootOption);

        command.Options.Add(InteractiveOption);

        command.SetAction(parseResult => new SdkInstallCommand(parseResult).Execute());

        return command;
    }
}
