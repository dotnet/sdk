// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;

internal static class RuntimeInstallCommandParser
{
    /// <summary>
    /// The runtime type to install (core, aspnetcore, windowsdesktop on Windows).
    /// </summary>
    public static readonly Argument<string> TypeArgument = new("type")
    {
        HelpName = "TYPE",
        Description = OperatingSystem.IsWindows()
            ? "The type of runtime to install: core, aspnetcore, or windowsdesktop"
            : "The type of runtime to install: core or aspnetcore",
        Arity = ArgumentArity.ExactlyOne, // eventually we'd support no type, which would install all 3 on windows, or core + aspnetcore concurrently on unix
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

    public static readonly Option<string> InstallPathOption = CommonOptions.InstallPathOption;
    public static readonly Option<bool?> SetDefaultInstallOption = CommonOptions.SetDefaultInstallOption;
    public static readonly Option<string> ManifestPathOption = CommonOptions.ManifestPathOption;
    public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption;
    public static readonly Option<bool> NoProgressOption = CommonOptions.NoProgressOption;
    public static readonly Option<bool> RequireMuxerUpdateOption = CommonOptions.RequireMuxerUpdateOption;

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
        command.Options.Add(RequireMuxerUpdateOption);

        command.SetAction(parseResult => new RuntimeInstallCommand(parseResult).Execute());

        return command;
    }
}
