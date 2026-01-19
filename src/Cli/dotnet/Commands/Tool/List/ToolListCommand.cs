// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.List;

internal sealed class ToolListCommand(
    ParseResult result,
    ToolListGlobalOrToolPathCommand? toolListGlobalOrToolPathCommand = null,
    ToolListLocalCommand? toolListLocalCommand = null)
    : CommandBase<ToolListCommandDefinition>(result)
{
    private readonly ToolListGlobalOrToolPathCommand _toolListGlobalOrToolPathCommand = toolListGlobalOrToolPathCommand ?? new(result);
    private readonly ToolListLocalCommand _toolListLocalCommand = toolListLocalCommand ?? new(result);

    public override int Execute()
    {
        Definition.LocationOptions.EnsureNoConflictGlobalLocalToolPathOption(
            _parseResult,
            CliCommandStrings.ListToolCommandInvalidGlobalAndLocalAndToolPath);

        CommandBase command = Definition.LocationOptions.IsGlobalOrToolPath(_parseResult)
            ? _toolListGlobalOrToolPathCommand
            : _toolListLocalCommand;

        return command.Execute();
    }
}
