// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.Common;

namespace Microsoft.DotNet.Tools.Tool.Install;

internal class ToolInstallCommand(
    ParseResult parseResult,
    ToolInstallGlobalOrToolPathCommand toolInstallGlobalOrToolPathCommand = null,
    ToolInstallLocalCommand toolInstallLocalCommand = null) : CommandBase(parseResult)
{
    private readonly ToolInstallLocalCommand _toolInstallLocalCommand = toolInstallLocalCommand;
    private readonly ToolInstallGlobalOrToolPathCommand _toolInstallGlobalOrToolPathCommand = toolInstallGlobalOrToolPathCommand;
    private readonly bool _global = parseResult.GetValue(ToolAppliedOption.GlobalOption);
    private readonly string _toolPath = parseResult.GetValue(ToolAppliedOption.ToolPathOption);
    private readonly string _framework = parseResult.GetValue(ToolInstallCommandParser.FrameworkOption);

    public override int Execute()
    {
        ToolAppliedOption.EnsureNoConflictGlobalLocalToolPathOption(
            _parseResult,
            LocalizableStrings.InstallToolCommandInvalidGlobalAndLocalAndToolPath);

        ToolAppliedOption.EnsureToolManifestAndOnlyLocalFlagCombination(
            _parseResult);

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
                        LocalizableStrings.LocalOptionDoesNotSupportFrameworkOption));
            }

            return (_toolInstallLocalCommand ?? new ToolInstallLocalCommand(_parseResult)).Execute();
        }
    }
}
