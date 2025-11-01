// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Tool.Dsc;

internal class ToolDscSetCommand : CommandBase
{
    private readonly string _input;

    public ToolDscSetCommand(ParseResult parseResult)
        : base(parseResult)
    {
        _input = parseResult.GetValue(ToolDscSetCommandParser.InputOption);
    }

    public override int Execute()
    {
        try
        {
            DscToolsState inputState = null;

            if (!string.IsNullOrEmpty(_input))
            {
                try
                {
                    string jsonInput = DscWriter.ReadInput(_input);
                    inputState = JsonSerializer.Deserialize<DscToolsState>(jsonInput);
                    DscWriter.WriteTrace($"Input JSON deserialized: {inputState?.Tools?.Count ?? 0} tools");
                }
                catch (JsonException ex)
                {
                    DscWriter.WriteError($"Failed to deserialize JSON: {ex.Message}");
                    return 1;
                }
            }

            foreach (var tool in inputState?.Tools ?? Enumerable.Empty<DscToolState>())
            {
                if (string.IsNullOrEmpty(tool.PackageId))
                {
                    DscWriter.WriteError("Property 'packageId' is required for 'set' operation.");
                    return 1;
                }

                if (tool.Exist == false)
                {
                    DscWriter.WriteError($"Removing tools is not supported. Use 'dotnet tool uninstall' instead.");
                    return 1;
                }

                DscWriter.WriteDebug($"Setting desired state for tool: {tool.PackageId}");

                // TODO: Implement actual tool installation/update logic
                // This would involve:
                // - Checking if tool is already installed
                // - Installing tool if not present
                // - Updating tool if version doesn't match
                // - Handling different scopes (global, local, toolPath)
            }

            var resultState = new DscToolsState();
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
