// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool;

internal readonly struct ToolLocationOptions(
    string globalOptionDescription,
    string localOptionDescription,
    string toolPathOptionDescription)
{
    public readonly Option<bool> GlobalOption = ToolAppliedOption.CreateGlobalOption(globalOptionDescription);
    public readonly Option<bool> LocalOption = ToolAppliedOption.CreateLocalOption(localOptionDescription);
    public readonly Option<string> ToolPathOption = ToolAppliedOption.CreateToolPathOption(toolPathOptionDescription);

    public void AddTo(IList<Option> options)
    {
        options.Add(GlobalOption);
        options.Add(LocalOption);
        options.Add(ToolPathOption);
    }

    public bool IsGlobalOrToolPath(ParseResult parseResult)
        => parseResult.HasOption(GlobalOption) || parseResult.HasOption(ToolPathOption);

    public void EnsureNoConflictGlobalLocalToolPathOption(ParseResult parseResult, string message)
    {
        List<string> options = [];
        if (parseResult.HasOption(GlobalOption))
        {
            options.Add(GlobalOption.Name);
        }

        if (parseResult.HasOption(LocalOption.Name))
        {
            options.Add(LocalOption.Name);
        }

        if (parseResult.HasOption(ToolPathOption))
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

    public void EnsureToolManifestAndOnlyLocalFlagCombination(ParseResult parseResult, Option<string> toolManifestOption)
    {
        if (IsGlobalOrToolPath(parseResult) &&
            parseResult.HasOption(toolManifestOption))
        {
            throw new GracefulException(
                string.Format(
                    CommandDefinitionStrings.OnlyLocalOptionSupportManifestFileOption));
        }
    }
}
