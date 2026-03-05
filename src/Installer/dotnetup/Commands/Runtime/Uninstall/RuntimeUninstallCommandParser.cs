// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Uninstall;

internal static class RuntimeUninstallCommandParser
{
    /// <summary>
    /// The component@version specification for which runtime to uninstall.
    /// Examples:
    ///   - "9.0" - uninstalls core runtime 9.0 channel spec
    ///   - "aspnetcore@10.0" - uninstalls ASP.NET Core 10.0 channel spec
    ///   - "windowsdesktop@9.0" - uninstalls Windows Desktop 9.0 channel spec
    /// </summary>
    public static readonly Argument<string> ComponentSpecArgument = new("component-spec")
    {
        HelpName = "COMPONENT_SPEC",
        Description = "The version/channel (e.g., 9.0) or component@version (e.g., aspnetcore@10.0) to uninstall. "
            + "When only a version is provided, the core .NET runtime install spec is targeted. "
            + "Valid component types: " + string.Join(", ", Install.RuntimeInstallCommand.GetValidRuntimeTypes()),
    };

    internal static readonly Option<InstallSource> s_sourceOption = new("--source")
    {
        Description = "Filter by install source (explicit, globaljson, all).",
        DefaultValueFactory = _ => InstallSource.Explicit
    };

    public static Option<InstallSource> SourceOption => s_sourceOption;

    public static readonly Option<string> ManifestPathOption = CommonOptions.ManifestPathOption;

    public static readonly Option<string> InstallPathOption = CommonOptions.InstallPathOption;

    public static readonly Option<bool> NoProgressOption = CommonOptions.NoProgressOption;

    private static readonly Command s_runtimeUninstallCommand = ConstructCommand();

    public static Command GetRuntimeUninstallCommand()
    {
        return s_runtimeUninstallCommand;
    }

    private static readonly Command s_rootUninstallCommand = ConstructCommand();

    public static Command GetRootUninstallCommand()
    {
        return s_rootUninstallCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("uninstall", "Removes a .NET Runtime install spec and cleans up unused installations");

        command.Arguments.Add(ComponentSpecArgument);
        command.Options.Add(SourceOption);
        command.Options.Add(ManifestPathOption);
        command.Options.Add(InstallPathOption);
        command.Options.Add(NoProgressOption);

        command.SetAction(parseResult => new RuntimeUninstallCommand(parseResult).Execute());

        return command;
    }
}
