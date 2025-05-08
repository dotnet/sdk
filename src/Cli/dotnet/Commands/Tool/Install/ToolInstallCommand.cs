// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Tool.Install;

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


    internal static void EnsureNoConflictPackageIdentityVersionOption(ParseResult parseResult)
    {
        if (!string.IsNullOrEmpty(parseResult.GetValue(ToolInstallCommandParser.PackageIdentityArgument)?.Version?.ToString()) &&
            !string.IsNullOrEmpty(parseResult.GetValue(ToolInstallCommandParser.VersionOption)))
        {
            throw new GracefulException(CliStrings.PackageIdentityArgumentVersionOptionConflict);
        }
    }

    public override int Execute()
    {
        ToolAppliedOption.EnsureNoConflictGlobalLocalToolPathOption(
            _parseResult,
            CliCommandStrings.InstallToolCommandInvalidGlobalAndLocalAndToolPath);

        ToolAppliedOption.EnsureToolManifestAndOnlyLocalFlagCombination(
            _parseResult);

        EnsureNoConflictPackageIdentityVersionOption(_parseResult);

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
