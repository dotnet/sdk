// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Uninstall;

internal sealed class ToolUninstallCommand : CommandBase<ToolUninstallCommandDefinition>
{
    private readonly ToolUninstallLocalCommand _toolUninstallLocalCommand;
    private readonly ToolUninstallGlobalOrToolPathCommand _toolUninstallGlobalOrToolPathCommand;
    private readonly bool _global;
    private readonly string _toolPath;

    public ToolUninstallCommand(
        ParseResult result,
        ToolUninstallGlobalOrToolPathCommand toolUninstallGlobalOrToolPathCommand = null,
        ToolUninstallLocalCommand toolUninstallLocalCommand = null)
        : base(result)
    {
        _toolUninstallLocalCommand = toolUninstallLocalCommand ?? new ToolUninstallLocalCommand(result);
        _toolUninstallGlobalOrToolPathCommand = toolUninstallGlobalOrToolPathCommand ?? new ToolUninstallGlobalOrToolPathCommand(result);

        _global = result.GetValue(Definition.LocationOptions.GlobalOption);
        _toolPath = result.GetValue(Definition.LocationOptions.ToolPathOption);
    }

    public override int Execute()
    {
        Definition.LocationOptions.EnsureNoConflictGlobalLocalToolPathOption(
            _parseResult,
            CliCommandStrings.UninstallToolCommandInvalidGlobalAndLocalAndToolPath);

        Definition.LocationOptions.EnsureToolManifestAndOnlyLocalFlagCombination(
            _parseResult,
            Definition.ToolManifestOption);

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
