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
        Description = CliCommandStrings.UpdateAllOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static Option<string> CreateVersionOption() => new("--version")
    {
        Description = CliCommandStrings.ToolInstallVersionOptionDescription,
        HelpName = CliCommandStrings.ToolInstallVersionOptionName
    };

    public static Option<string> CreateToolPathOption(string description) => new("--tool-path")
    {
        HelpName = CliCommandStrings.ToolInstallToolPathOptionName,
        Description = description,
    };

    public static Option<string> CreateToolManifestOption(string description) => new("--tool-manifest")
    {
        HelpName = CliCommandStrings.ToolInstallManifestPathOptionName,
        Arity = ArgumentArity.ZeroOrOne,
        Description = description,
    };

    public static Option<bool> CreatePrereleaseOption() => new("--prerelease")
    {
        Description = CliCommandStrings.ToolSearchPrereleaseDescription,
        Arity = ArgumentArity.Zero
    };

    public static Option<bool> CreateRollForwardOption() => new("--allow-roll-forward")
    {
        Description = CliCommandStrings.RollForwardOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static Option<string> CreateConfigOption() => new("--configfile")
    {
        Description = CliCommandStrings.ToolInstallConfigFileOptionDescription,
        HelpName = CliCommandStrings.ToolInstallConfigFileOptionName
    };

    public static Option<string[]> CreateSourceOption() => new Option<string[]>("--source")
    {
        Description = CliCommandStrings.ToolInstallSourceOptionDescription,
        HelpName = CliCommandStrings.ToolInstallSourceOptionName
    }.AllowSingleArgPerToken();

    public static Option<string[]> CreateAddSourceOption() => new Option<string[]>("--add-source")
    {
        Description = CliCommandStrings.ToolInstallAddSourceOptionDescription,
        HelpName = CliCommandStrings.ToolInstallAddSourceOptionName
    }.AllowSingleArgPerToken();

    public static Option<bool> CreateAllowPackageDowngradeOption() => new("--allow-downgrade")
    {
        Description = CliCommandStrings.AllowPackageDowngradeOptionDescription,
        Arity = ArgumentArity.Zero
    };
}
