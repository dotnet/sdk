// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;

internal static class RuntimeInstallCommandParser
{
    /// <summary>
    /// The runtime type to install (core, aspnetcore, windowsdesktop).
    /// </summary>
    public static readonly Argument<string> TypeArgument = new("type")
    {
        HelpName = "TYPE",
        Description = "The type of runtime to install: core, aspnetcore, or windowsdesktop",
        Arity = ArgumentArity.ExactlyOne,
    };

    /// <summary>
    /// The channel or version to install.
    /// </summary>
    public static readonly Argument<string?> ChannelArgument = new("channel")
    {
        HelpName = "CHANNEL",
        Description = "The channel of the .NET Runtime to install. For example: latest, 10, or 9.0. A specific version (for example 9.0.0) can also be specified.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static readonly Option<string> InstallPathOption = new("--install-path")
    {
        HelpName = "INSTALL_PATH",
        Description = "The path to install the .NET Runtime to",
    };

    public static readonly Option<bool?> SetDefaultInstallOption = new("--set-default-install")
    {
        Description = "Set the install path as the default dotnet install. This will update the PATH and DOTNET_ROOT environment variables.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = r => null
    };

    public static readonly Option<string> ManifestPathOption = new("--manifest-path")
    {
        HelpName = "MANIFEST_PATH",
        Description = "Custom path to the manifest file for tracking .NET Runtime installations",
    };

    public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption;
    public static readonly Option<bool> NoProgressOption = CommonOptions.NoProgressOption;

    private static readonly Command RuntimeInstallCommand = ConstructCommand();

    public static Command GetRuntimeInstallCommand()
    {
        return RuntimeInstallCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("install", "Installs a .NET Runtime");

        command.Arguments.Add(TypeArgument);
        command.Arguments.Add(ChannelArgument);

        command.Options.Add(InstallPathOption);
        command.Options.Add(SetDefaultInstallOption);
        command.Options.Add(ManifestPathOption);

        command.Options.Add(InteractiveOption);
        command.Options.Add(NoProgressOption);

        command.SetAction(parseResult => new RuntimeInstallCommand(parseResult).Execute());

        return command;
    }
}
