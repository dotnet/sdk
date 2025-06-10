// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Tool.Common;

internal class ToolAppliedOption
{
    private const string GlobalOptionName = "--global";
    private const string LocalOptionName = "--local";
    private const string ToolPathName = "--tool-path";
    private const string ToolManifestName = "--tool-manifest";

    public static Option<bool> GlobalOption(string description) => new(GlobalOptionName, "-g")
    {
        Arity = ArgumentArity.Zero,
        Description = description
    };

    public static Option<bool> LocalOption(string description) => new(LocalOptionName)
    {
        Arity = ArgumentArity.Zero,
        Description = description,
    };

    public static Option<bool> UpdateAllOption = new("--all")
    {
        Description = CliCommandStrings.UpdateAllOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<string> VersionOption
        = ToolInstallCommandParser.VersionOption
          ?? new("--version"); // Workaround for Mono runtime (https://github.com/dotnet/sdk/issues/41672)

    public static Option<string> ToolPathOption(string description) => new(ToolPathName)
    {
        HelpName = CliCommandStrings.ToolInstallToolPathOptionName,
        Description = description,
    };

    public static Option<string> ToolManifestOption(string description) => new(ToolManifestName)
    {
        HelpName = CliCommandStrings.ToolInstallManifestPathOptionName,
        Arity = ArgumentArity.ZeroOrOne,
        Description = description,
    };

    internal static void EnsureNoConflictGlobalLocalToolPathOption(
        ParseResult parseResult,
        string message)
    {
        List<string> options = [];
        if (parseResult.GetResult(GlobalOptionName) is not null)
        {
            options.Add(GlobalOptionName);
        }

        if (parseResult.GetResult(LocalOptionName) is not null)
        {
            options.Add(LocalOptionName);
        }

        if (parseResult.GetResult(ToolPathName) is not null)
        {
            options.Add(ToolPathName);
        }

        if (options.Count > 1)
        {
            throw new GracefulException(
                string.Format(
                    message,
                    string.Join(" ", options)));
        }
    }

    internal static void EnsureNoConflictUpdateAllVersionOption(
        ParseResult parseResult,
        string message)
    {
        List<string> options = [];
        if (parseResult.GetResult(UpdateAllOption) is not null)
        {
            options.Add(UpdateAllOption.Name);
        }

        if (parseResult.GetResult(VersionOption) is not null)
        {
            options.Add(VersionOption.Name);
        }

        if (options.Count > 1)
        {
            throw new GracefulException(
                string.Format(
                    message,
                    string.Join(" ", options)));
        }
    }

    internal static void EnsureToolManifestAndOnlyLocalFlagCombination(ParseResult parseResult)
    {
        if (GlobalOrToolPath(parseResult) &&
            parseResult.GetResult(ToolManifestName) is not null)
        {
            throw new GracefulException(
                string.Format(
                    CliCommandStrings.OnlyLocalOptionSupportManifestFileOption));
        }
    }

    private static bool GlobalOrToolPath(ParseResult parseResult)
        => parseResult.GetResult(GlobalOptionName) is not null
        || parseResult.GetResult(ToolPathName) is not null;
}
