// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Update;

internal static class RuntimeUpdateCommandParser
{
    /// <summary>
    /// Optional component spec to filter which runtime(s) to update.
    /// Examples:
    ///   - null/empty: update all runtime-type install specs
    ///   - "runtime": update only core runtime specs
    ///   - "aspnetcore": update only ASP.NET Core specs
    ///   - "windowsdesktop": update only Windows Desktop specs
    /// </summary>
    public static readonly Argument<string?> ComponentSpecArgument = new("component-spec")
    {
        HelpName = "COMPONENT_SPEC",
        Description = "Optional runtime component type to update (e.g., runtime, aspnetcore, windowsdesktop). "
            + "When omitted, all runtime-type install specs are updated. "
            + "Valid types: " + string.Join(", ", Install.RuntimeInstallCommand.GetValidRuntimeTypes()),
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static readonly Option<string> ManifestPathOption = CommonOptions.ManifestPathOption;

    public static readonly Option<string> InstallPathOption = CommonOptions.InstallPathOption;

    public static readonly Option<bool> NoProgressOption = CommonOptions.NoProgressOption;

    private static readonly Command s_runtimeUpdateCommand = ConstructCommand();

    public static Command GetRuntimeUpdateCommand()
    {
        return s_runtimeUpdateCommand;
    }

    private static readonly Command s_rootUpdateCommand = ConstructCommand();

    public static Command GetRootUpdateCommand()
    {
        return s_rootUpdateCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("update", "Updates managed .NET Runtime installations to the latest versions");

        command.Arguments.Add(ComponentSpecArgument);
        command.Options.Add(ManifestPathOption);
        command.Options.Add(InstallPathOption);
        command.Options.Add(NoProgressOption);

        command.SetAction(parseResult => new RuntimeUpdateCommand(parseResult).Execute());

        return command;
    }
}
