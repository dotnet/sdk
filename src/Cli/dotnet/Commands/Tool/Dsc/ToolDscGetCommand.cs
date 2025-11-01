// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Tool.Dsc;

internal class ToolDscGetCommand : CommandBase
{
    private readonly string _input;

    public ToolDscGetCommand(ParseResult parseResult)
        : base(parseResult)
    {
        _input = parseResult.GetValue(ToolDscGetCommandParser.InputOption);
    }

    public override int Execute()
    {
        try
        {
            var inputState = DscWriter.ReadAndDeserializeInput(_input);
            var resultState = new DscToolsState();

            foreach (var tool in inputState?.Tools ?? Enumerable.Empty<DscToolState>())
            {
                var actualState = DscWriter.QueryToolState(tool);
                if (actualState != null)
                {
                    resultState.Tools.Add(actualState);
                }
            }

            DscWriter.WriteResult(resultState);

            return 0;
        }
        catch (Exception ex)
        {
            DscWriter.WriteError($"Unexpected error: {ex.Message}");
            return 1;
        }
    }
}
