// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Tool.Common;

internal class ToolAppliedOption
{
    public static Option<bool> GlobalOption = new("--global", "-g")
    {
        Arity = ArgumentArity.Zero
    };

    public static Option<bool> LocalOption = new("--local")
    {
        Arity = ArgumentArity.Zero
    };

    public static Option<bool> UpdateAllOption = new("--all")
    {
        Description = CliCommandStrings.UpdateAllOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<string> VersionOption
        = ToolInstallCommandParser.VersionOption
          ?? new("--version"); // Workaround for Mono runtime (https://github.com/dotnet/sdk/issues/41672)

    public static Option<string> ToolPathOption = new("--tool-path")
    {
        HelpName = CliCommandStrings.ToolInstallToolPathOptionName
    };

    public static Option<string> ToolManifestOption = new("--tool-manifest")
    {
        HelpName = CliCommandStrings.ToolInstallManifestPathOptionName,
        Arity = ArgumentArity.ZeroOrOne
    };

    internal static void EnsureNoConflictGlobalLocalToolPathOption(
        ParseResult parseResult,
        string message)
    {
        List<string> options = [];
        if (parseResult.GetResult(GlobalOption) is not null)
        {
            options.Add(GlobalOption.Name);
        }

        if (parseResult.GetResult(LocalOption) is not null)
        {
            options.Add(LocalOption.Name);
        }

        if (!string.IsNullOrWhiteSpace(parseResult.GetValue(ToolPathOption)))
        {
            options.Add(ToolPathOption.Name);
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
            !string.IsNullOrWhiteSpace(parseResult.GetValue(ToolManifestOption)))
        {
            throw new GracefulException(
                string.Format(
                    CliCommandStrings.OnlyLocalOptionSupportManifestFileOption));
        }
    }

    private static bool GlobalOrToolPath(ParseResult parseResult)
    {
        return parseResult.GetResult(GlobalOption) is not null ||
               !string.IsNullOrWhiteSpace(parseResult.GetValue(ToolPathOption));
    }
}
