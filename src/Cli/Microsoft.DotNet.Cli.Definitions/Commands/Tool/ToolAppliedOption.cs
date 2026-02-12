// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool;

internal static class ToolAppliedOption
{
    public static Option<bool> CreateGlobalOption(string description) => new("--global", "-g")
    {
        Arity = ArgumentArity.Zero,
        Description = description
    };

    public static Option<bool> CreateLocalOption(string description) => new("--local")
    {
        Arity = ArgumentArity.Zero,
        Description = description,
    };

    public static Option<bool> CreateUpdateAllOption() => new("--all")
    {
        Description = CommandDefinitionStrings.UpdateAllOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static Option<string> CreateVersionOption() => new("--version")
    {
        Description = CommandDefinitionStrings.ToolInstallVersionOptionDescription,
        HelpName = CommandDefinitionStrings.ToolInstallVersionOptionName
    };

    public static Option<string> CreateToolPathOption(string description) => new("--tool-path")
    {
        HelpName = CommandDefinitionStrings.ToolInstallToolPathOptionName,
        Description = description,
    };

    public static Option<string> CreateToolManifestOption(string description) => new("--tool-manifest")
    {
        HelpName = CommandDefinitionStrings.ToolInstallManifestPathOptionName,
        Arity = ArgumentArity.ZeroOrOne,
        Description = description,
    };

    public static Option<bool> CreatePrereleaseOption() => new("--prerelease")
    {
        Description = CommandDefinitionStrings.ToolSearchPrereleaseDescription,
        Arity = ArgumentArity.Zero
    };

    public static Option<bool> CreateRollForwardOption() => new("--allow-roll-forward")
    {
        Description = CommandDefinitionStrings.RollForwardOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static Option<string> CreateConfigOption() => new("--configfile")
    {
        Description = CommandDefinitionStrings.ToolInstallConfigFileOptionDescription,
        HelpName = CommandDefinitionStrings.ToolInstallConfigFileOptionName
    };

    public static Option<string[]> CreateSourceOption() => new Option<string[]>("--source")
    {
        Description = CommandDefinitionStrings.ToolInstallSourceOptionDescription,
        HelpName = CommandDefinitionStrings.ToolInstallSourceOptionName
    }.AllowSingleArgPerToken();

    public static Option<string[]> CreateAddSourceOption() => new Option<string[]>("--add-source")
    {
        Description = CommandDefinitionStrings.ToolInstallAddSourceOptionDescription,
        HelpName = CommandDefinitionStrings.ToolInstallAddSourceOptionName
    }.AllowSingleArgPerToken();

    public static Option<bool> CreateAllowPackageDowngradeOption() => new("--allow-downgrade")
    {
        Description = CommandDefinitionStrings.AllowPackageDowngradeOptionDescription,
        Arity = ArgumentArity.Zero
    };
}
