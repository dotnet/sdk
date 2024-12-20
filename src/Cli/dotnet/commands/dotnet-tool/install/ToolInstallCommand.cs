// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.Common;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal class ToolInstallCommand : CommandBase
    {
        private readonly ToolInstallLocalCommand _toolInstallLocalCommand;
        private readonly ToolInstallGlobalOrToolPathCommand _toolInstallGlobalOrToolPathCommand;
        private readonly bool _global;
        private readonly string _toolPath;
        private readonly string _framework;

        public ToolInstallCommand(
            ParseResult parseResult,
            ToolInstallGlobalOrToolPathCommand toolInstallGlobalOrToolPathCommand = null,
            ToolInstallLocalCommand toolInstallLocalCommand = null)
            : base(parseResult)
        {
            _toolInstallLocalCommand = toolInstallLocalCommand;

            _toolInstallGlobalOrToolPathCommand = toolInstallGlobalOrToolPathCommand;

            _global = parseResult.GetValue(ToolAppliedOption.GlobalOption);
            _toolPath = parseResult.GetValue(ToolAppliedOption.ToolPathOption);
            _framework = parseResult.GetValue(ToolInstallCommandParser.FrameworkOption);
        }

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
}
