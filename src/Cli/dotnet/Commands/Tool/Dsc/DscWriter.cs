// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.Commands.Tool.Dsc;

internal static class DscWriter
{
    /// <summary>
    /// Writes an error message to stderr in DSC JSON format.
    /// </summary>
    public static void WriteError(string message)
    {
        var errorMessage = new DscErrorMessage { Error = message };
        string json = JsonSerializer.Serialize(errorMessage);
        Reporter.Error.WriteLine(json);
    }

    /// <summary>
    /// Writes a debug message to stderr in DSC JSON format.
    /// </summary>
    public static void WriteDebug(string message)
    {
        var debugMessage = new DscDebugMessage { Debug = message };
        string json = JsonSerializer.Serialize(debugMessage);
        Reporter.Error.WriteLine(json);
    }

    /// <summary>
    /// Writes a trace message to stderr in DSC JSON format.
    /// </summary>
    public static void WriteTrace(string message)
    {
        var traceMessage = new DscTraceMessage { Trace = message };
        string json = JsonSerializer.Serialize(traceMessage);
        Reporter.Error.WriteLine(json);
    }

    /// <summary>
    /// Writes the result state to stdout in DSC JSON format.
    /// </summary>
    public static void WriteResult(DscToolsState state)
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        string json = JsonSerializer.Serialize(state, options);
        Reporter.Output.WriteLine(json);
    }

    /// <summary>
    /// Writes any object to stdout in JSON format.
    /// </summary>
    public static void WriteJson(object obj, bool writeIndented = false)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented
        };

        string json = JsonSerializer.Serialize(obj, options);
        Reporter.Output.WriteLine(json);
    }

    /// <summary>
    /// Reads input from either stdin (if input is "-") or from a file.
    /// </summary>
    public static string ReadInput(string input)
    {
        if (input == "-")
        {
            return Console.In.ReadToEnd();
        }
        else
        {
            return File.ReadAllText(input);
        }
    }

    /// <summary>
    /// Reads and deserializes DSC tool state from input (file or stdin).
    /// Returns null if input is not provided.
    /// Exits with code 1 if deserialization fails.
    /// </summary>
    public static DscToolsState? ReadAndDeserializeInput(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }

        try
        {
            string jsonInput = ReadInput(input);
            var inputState = JsonSerializer.Deserialize<DscToolsState>(jsonInput);
            return inputState;
        }
        catch (JsonException ex)
        {
            WriteError($"Failed to deserialize JSON: {ex.Message}");
            Environment.Exit(1);
            return null; // Unreachable, but satisfies compiler
        }
    }

    /// <summary>
    /// Queries the actual state of a tool from the package store.
    /// </summary>
    public static DscToolState QueryToolState(DscToolState requestedState)
    {
        // Parse packageId to handle packageId@version syntax
        var (packageIdString, _) = requestedState.ParsePackageIdentity();
        
        if (string.IsNullOrEmpty(packageIdString))
        {
            packageIdString = requestedState.PackageId ?? string.Empty;
        }

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
            WriteDebug($"Local tool scope not yet implemented for {packageIdString}");
            return new DscToolState
            {
                PackageId = packageIdString,
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
        var packageId = new PackageId(packageIdString);

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

                WriteDebug($"Found tool {package.Id} version {package.Version.ToNormalizedString()}");

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
                WriteDebug($"Tool {packageIdString} not found in {scope} scope");

                return new DscToolState
                {
                    PackageId = packageIdString,
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
            WriteError($"Error querying tool {packageIdString}: {ex.Message}");

            return new DscToolState
            {
                PackageId = packageIdString,
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
