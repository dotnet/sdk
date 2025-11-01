// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

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

            var resultState = new DscToolsState();

            foreach (var tool in inputState?.Tools ?? Enumerable.Empty<DscToolState>())
            {
                if (string.IsNullOrEmpty(tool.PackageId))
                {
                    DscWriter.WriteError("Property 'packageId' is required for 'get' operation.");
                    return 1;
                }

                var actualState = GetToolState(tool);
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

    private DscToolState GetToolState(DscToolState requestedState)
    {
        // Determine the scope and tool path based on the requested tool
        DirectoryPath? toolPath = null;
        DscToolScope scope = requestedState.Scope ?? DscToolScope.Global;

        if (scope == DscToolScope.ToolPath && !string.IsNullOrWhiteSpace(requestedState.ToolPath))
        {
            toolPath = new DirectoryPath(requestedState.ToolPath);
        }
        else if (scope == DscToolScope.Global)
        {
            // Global tools, use default location (null)
            toolPath = null;
        }
        else if (scope == DscToolScope.Local)
        {
            // TODO: Local tools require querying dotnet-tools.json in current directory
            // For now, return not found for local tools
            DscWriter.WriteTrace($"Local tool scope not yet implemented for {requestedState.PackageId}");
            return new DscToolState
            {
                PackageId = requestedState.PackageId,
                Version = null,
                Commands = null,
                Scope = DscToolScope.Local,
                ToolPath = null,
                ManifestPath = null,
                Exist = false
            };
        }

        // Query the tool package store
        var packageStoreQuery = ToolPackageFactory.CreateToolPackageStoreQuery(toolPath);
        var packageId = new PackageId(requestedState.PackageId);

        try
        {
            // Find the tool package
            var installedPackages = packageStoreQuery.EnumeratePackages()
                .Where(p => p.Id.Equals(packageId))
                .ToList();

            if (installedPackages.Any())
            {
                // Tool exists, get its details from the first (or only) matching package
                var package = installedPackages.First();

                DscWriter.WriteTrace($"Found tool {package.Id} version {package.Version.ToNormalizedString()}");

                return new DscToolState
                {
                    PackageId = package.Id.ToString(),
                    Version = package.Version.ToNormalizedString(),
                    Commands = package.Command != null ? new List<string> { package.Command.Name.Value } : null,
                    Scope = scope,
                    ToolPath = scope == DscToolScope.ToolPath ? requestedState.ToolPath : null,
                    ManifestPath = null,
                    Exist = true
                };
            }
            else
            {
                // Tool not found
                DscWriter.WriteTrace($"Tool {requestedState.PackageId} not found in {scope} scope");

                return new DscToolState
                {
                    PackageId = requestedState.PackageId,
                    Version = null,
                    Commands = null,
                    Scope = scope,
                    ToolPath = scope == DscToolScope.ToolPath ? requestedState.ToolPath : null,
                    ManifestPath = null,
                    Exist = false
                };
            }
        }
        catch (Exception ex)
        {
            // If there's an error querying the tool, return it as not found
            DscWriter.WriteError($"Error querying tool {requestedState.PackageId}: {ex.Message}");

            return new DscToolState
            {
                PackageId = requestedState.PackageId,
                Version = null,
                Commands = null,
                Scope = scope,
                ToolPath = scope == DscToolScope.ToolPath ? requestedState.ToolPath : null,
                ManifestPath = null,
                Exist = false
            };
        }
    }

}
