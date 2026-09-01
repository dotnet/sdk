// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Tool.Install;

internal sealed class ToolInstallCommand : CommandBase<ToolInstallCommandDefinition>
{
    private readonly ToolInstallLocalCommand _toolInstallLocalCommand;
    private readonly ToolInstallGlobalOrToolPathCommand _toolInstallGlobalOrToolPathCommand;
    private readonly bool _global;
    private readonly string _toolPath;
    private readonly string _framework;

    public ToolInstallCommand(
        ParseResult parseResult,
        ToolInstallGlobalOrToolPathCommand toolInstallGlobalOrToolPathCommand = null,
        ToolInstallLocalCommand toolInstallLocalCommand = null) : base(parseResult)
    {
        _toolInstallLocalCommand = toolInstallLocalCommand;
        _toolInstallGlobalOrToolPathCommand = toolInstallGlobalOrToolPathCommand;
        _global = parseResult.GetValue(Definition.LocationOptions.GlobalOption);
        _toolPath = parseResult.GetValue(Definition.LocationOptions.ToolPathOption);
        _framework = parseResult.GetValue(Definition.FrameworkOption);
    }

    public override int Execute()
    {
        Definition.LocationOptions.EnsureNoConflictGlobalLocalToolPathOption(
            _parseResult,
            CliCommandStrings.InstallToolCommandInvalidGlobalAndLocalAndToolPath);

        Definition.LocationOptions.EnsureToolManifestAndOnlyLocalFlagCombination(
            _parseResult,
            Definition.ToolManifestOption);

        if (_global || !string.IsNullOrWhiteSpace(_toolPath))
        {
            return (_toolInstallGlobalOrToolPathCommand ?? new ToolInstallGlobalOrToolPathCommand(_parseResult)).Execute();
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(_framework))
            {
                throw new GracefulException(
                    string.Format(
                        CliCommandStrings.LocalOptionDoesNotSupportFrameworkOption));
            }

            return (_toolInstallLocalCommand ?? new ToolInstallLocalCommand(_parseResult)).Execute();
        }
    }
}
