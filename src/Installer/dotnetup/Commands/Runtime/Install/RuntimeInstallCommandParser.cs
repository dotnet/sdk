// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;

internal static class RuntimeInstallCommandParser
{
    /// <summary>
    /// The version/channel or component@version specification.
    /// Examples:
    ///   - "10.0.1" - installs core runtime version 10.0.1
    ///   - "latest" - installs latest core runtime
    ///   - "aspnetcore@10.0.1" - installs ASP.NET Core runtime 10.0.1
    ///   - "windowsdesktop@9.0" - installs Windows Desktop runtime for 9.0 channel
    /// </summary>
    public static readonly Argument<string?> ComponentSpecArgument = new("component-spec")
    {
        HelpName = "COMPONENT_SPEC",
        Description = "The version/channel (e.g., 10.0.1, latest) or component@version (e.g., aspnetcore@10.0.1, windowsdesktop@9.0). "
            + "When only a version is provided, the core .NET runtime is installed. "
            + "Component types: runtime, aspnetcore" + (OperatingSystem.IsWindows() ? ", windowsdesktop" : ""),
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static readonly Option<string> InstallPathOption = CommonOptions.InstallPathOption;
    public static readonly Option<bool?> SetDefaultInstallOption = CommonOptions.SetDefaultInstallOption;
    public static readonly Option<string> ManifestPathOption = CommonOptions.ManifestPathOption;
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

        command.Arguments.Add(ComponentSpecArgument);

        command.Options.Add(InstallPathOption);
        command.Options.Add(SetDefaultInstallOption);
        command.Options.Add(ManifestPathOption);

        command.Options.Add(InteractiveOption);
        command.Options.Add(NoProgressOption);

        command.SetAction(parseResult => new RuntimeInstallCommand(parseResult).Execute());

        return command;
    }
}
