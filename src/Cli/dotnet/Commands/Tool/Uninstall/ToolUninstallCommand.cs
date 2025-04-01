// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Tool.Common;

namespace Microsoft.DotNet.Tools.Tool.Uninstall;

internal class ToolUninstallCommand(
    ParseResult result,
    ToolUninstallGlobalOrToolPathCommand toolUninstallGlobalOrToolPathCommand = null,
    ToolUninstallLocalCommand toolUninstallLocalCommand = null) : CommandBase(result)
{
    private readonly ToolUninstallLocalCommand _toolUninstallLocalCommand
            = toolUninstallLocalCommand ??
              new ToolUninstallLocalCommand(result);
    private readonly ToolUninstallGlobalOrToolPathCommand _toolUninstallGlobalOrToolPathCommand =
            toolUninstallGlobalOrToolPathCommand
            ?? new ToolUninstallGlobalOrToolPathCommand(result);
    private readonly bool _global = result.GetValue(ToolUninstallCommandParser.GlobalOption);
    private readonly string _toolPath = result.GetValue(ToolUninstallCommandParser.ToolPathOption);

    public override int Execute()
    {
        ToolAppliedOption.EnsureNoConflictGlobalLocalToolPathOption(
            _parseResult,
            LocalizableStrings.UninstallToolCommandInvalidGlobalAndLocalAndToolPath);

        ToolAppliedOption.EnsureToolManifestAndOnlyLocalFlagCombination(_parseResult);

        if (_global || !string.IsNullOrWhiteSpace(_toolPath))
        {
            return _toolUninstallGlobalOrToolPathCommand.Execute();
        }
        else
        {
            return _toolUninstallLocalCommand.Execute();
        }
    }
}
