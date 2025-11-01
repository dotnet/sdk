// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Tool.Dsc;

internal class ToolDscManifestCommand : CommandBase
{
    public ToolDscManifestCommand(ParseResult parseResult)
        : base(parseResult)
    {
    }

    public override int Execute()
    {
        try
        {
            var manifest = new Dictionary<string, object>
            {
                ["$schema"] = "https://aka.ms/dsc/schemas/v3/bundled/resource/manifest.json",
                ["type"] = "Microsoft.DotNet.Sdk/Tool",
                ["description"] = "Manage .NET tools using Microsoft Desired State Configuration (DSC).",
                ["version"] = "1.0.0",
                ["tags"] = new[] { "dotnet", "tool", "sdk" },
                ["exitCodes"] = new Dictionary<string, string>
                {
                    ["0"] = "Success",
                    ["1"] = "Error"
                },
                ["schema"] = new Dictionary<string, object>
                {
                    ["command"] = new Dictionary<string, object>
                    {
                        ["executable"] = "dotnet",
                        ["args"] = new object[] { "tool", "dsc", "schema" }
                    }
                },
                ["get"] = new Dictionary<string, object>
                {
                    ["executable"] = "dotnet",
                    ["args"] = new object[]
                    {
                        "tool",
                        "dsc",
                        "get",
                        new Dictionary<string, object>
                        {
                            ["jsonInputArg"] = "--input",
                            ["mandatory"] = true
                        }
                    }
                },
                ["set"] = new Dictionary<string, object>
                {
                    ["executable"] = "dotnet",
                    ["args"] = new object[]
                    {
                        "tool",
                        "dsc",
                        "set",
                        new Dictionary<string, object>
                        {
                            ["jsonInputArg"] = "--input",
                            ["mandatory"] = true
                        }
                    }
                },
                ["test"] = new Dictionary<string, object>
                {
                    ["executable"] = "dotnet",
                    ["args"] = new object[]
                    {
                        "tool",
                        "dsc",
                        "test",
                        new Dictionary<string, object>
                        {
                            ["jsonInputArg"] = "--input",
                            ["mandatory"] = true
                        }
                    }
                },
                ["export"] = new Dictionary<string, object>
                {
                    ["executable"] = "dotnet",
                    ["args"] = new object[] { "tool", "dsc", "export" }
                }
            };

            DscWriter.WriteJson(manifest);

            return 0;
        }
        catch (Exception ex)
        {
            DscWriter.WriteError($"Error generating manifest: {ex.Message}");
            return 1;
        }
    }
}
