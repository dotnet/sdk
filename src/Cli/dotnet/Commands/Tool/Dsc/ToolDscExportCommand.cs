// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.Commands.Tool.Dsc;

internal class ToolDscExportCommand : CommandBase
{
    public ToolDscExportCommand(ParseResult parseResult)
        : base(parseResult)
    {
    }

    public override int Execute()
    {
        try
        {
            var state = new DscToolsState();

            // Enumerate all global tools
            DscWriter.WriteTrace("Enumerating global tools");
            var globalTools = EnumerateGlobalTools();
            foreach (var tool in globalTools)
            {
                state.Tools.Add(tool);
            }

            DscWriter.WriteTrace($"Found {state.Tools.Count} global tools");

            // TODO: Add support for local tools (requires scanning for dotnet-tools.json files)
            // TODO: Add support for tool-path tools (requires configuration of tool paths to scan)

            DscWriter.WriteResult(state);

            return 0;
        }
        catch (Exception ex)
        {
            DscWriter.WriteError($"Unexpected error: {ex.Message}");
            return 1;
        }
    }

    private List<DscToolState> EnumerateGlobalTools()
    {
        var tools = new List<DscToolState>();

        // Query the global tool package store (null = default global location)
        var packageStoreQuery = ToolPackageFactory.CreateToolPackageStoreQuery(null);

        try
        {
            var packages = packageStoreQuery.EnumeratePackages();

            foreach (var package in packages)
            {
                try
                {
                    // Only include packages that have commands
                    if (package.Command != null)
                    {
                        tools.Add(new DscToolState
                        {
                            PackageId = package.Id.ToString(),
                            Version = package.Version.ToNormalizedString(),
                            Commands = new List<string> { package.Command.Name.Value },
                            Scope = DscToolScope.Global,
                            ToolPath = null,
                            ManifestPath = null,
                            Exist = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    // If we can't read a specific package, log a warning and continue
                    DscWriter.WriteError($"Warning: Could not read tool {package.Id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            DscWriter.WriteError($"Error enumerating global tools: {ex.Message}");
        }

        return tools;
    }
}
