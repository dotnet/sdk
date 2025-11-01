// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Tool.Dsc;

internal class ToolDscSchemaCommand : CommandBase
{
    public ToolDscSchemaCommand(ParseResult parseResult)
        : base(parseResult)
    {
    }

    public override int Execute()
    {
        try
        {
            // For now, return a basic schema
            // TODO: Generate proper JSON schema when System.Text.Json.Schema is available
            var schema = new
            {
                type = "object",
                properties = new
                {
                    tools = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                packageId = new { type = "string" },
                                version = new { type = "string" },
                                commands = new { type = "array", items = new { type = "string" } },
                                scope = new { type = "string", @enum = new[] { "Global", "Local", "ToolPath" } },
                                toolPath = new { type = "string" },
                                manifestPath = new { type = "string" },
                                _exist = new { type = "boolean" }
                            },
                            required = new[] { "packageId" }
                        }
                    }
                }
            };

            DscWriter.WriteJson(schema, writeIndented: true);

            return 0;
        }
        catch (Exception ex)
        {
            DscWriter.WriteError($"Unexpected error: {ex.Message}");
            return 1;
        }
    }
}
