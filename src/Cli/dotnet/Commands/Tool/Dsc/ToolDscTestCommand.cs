// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Tool.Dsc;

internal class ToolDscTestCommand : CommandBase
{
    private readonly string _input;

    public ToolDscTestCommand(ParseResult parseResult)
        : base(parseResult)
    {
        _input = parseResult.GetValue(ToolDscTestCommandParser.InputOption);
    }

    public override int Execute()
    {
        try
        {
            var inputState = DscWriter.ReadAndDeserializeInput(_input);
            var resultState = new DscToolsState();
            bool allMatch = true;

            foreach (var tool in inputState?.Tools ?? Enumerable.Empty<DscToolState>())
            {
                var actualState = DscWriter.QueryToolState(tool);
                bool matches = CompareToolStates(tool, actualState);
                
                actualState.Exist = matches;
                
                if (!matches)
                {
                    allMatch = false;
                }

                resultState.Tools.Add(actualState);
            }

            DscWriter.WriteResult(resultState);

            // Return exit code 0 if all tools match desired state, 1 if any don't match
            return allMatch ? 0 : 1;
        }
        catch (Exception ex)
        {
            DscWriter.WriteError($"Unexpected error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Compares desired state with actual state to determine if they match.
    /// </summary>
    private bool CompareToolStates(DscToolState desired, DscToolState actual)
    {
        // Parse packageId to handle packageId@version syntax
        var (packageId, versionRange) = desired.ParsePackageIdentity();

        // Check _exist property first
        bool desiredExist = desired.Exist ?? true; // Default to true if not specified
        bool actualExist = actual.Exist ?? false;

        if (desiredExist != actualExist)
        {
            DscWriter.WriteDebug($"Tool {packageId}: Exist mismatch (desired: {desiredExist}, actual: {actualExist})");
            return false;
        }

        // If tool should not exist and doesn't exist, that's a match
        if (!desiredExist && !actualExist)
        {
            DscWriter.WriteDebug($"Tool {packageId}: Correctly does not exist");
            return true;
        }

        // If tool should exist but doesn't, already caught above
        // Now check version if specified
        if (versionRange != null)
        {
            // packageId@version syntax or Version property specified
            if (string.IsNullOrEmpty(actual.Version))
            {
                DscWriter.WriteDebug($"Tool {packageId}: Version mismatch (desired: {versionRange.OriginalString}, actual: none)");
                return false;
            }

            // Check if actual version satisfies the version range
            if (NuGetVersion.TryParse(actual.Version, out var actualVersion))
            {
                if (!versionRange.Satisfies(actualVersion))
                {
                    DscWriter.WriteDebug($"Tool {packageId}: Version mismatch (desired: {versionRange.OriginalString}, actual: {actual.Version})");
                    return false;
                }
            }
            else
            {
                DscWriter.WriteDebug($"Tool {packageId}: Failed to parse actual version {actual.Version}");
                return false;
            }
        }
        else if (!string.IsNullOrEmpty(desired.Version))
        {
            // Fallback to string comparison for Version property (shouldn't happen if ParsePackageIdentity works)
            if (string.IsNullOrEmpty(actual.Version))
            {
                DscWriter.WriteDebug($"Tool {packageId}: Version mismatch (desired: {desired.Version}, actual: none)");
                return false;
            }

            if (NuGetVersion.TryParse(desired.Version, out var desiredVersion) &&
                NuGetVersion.TryParse(actual.Version, out var actualVersion))
            {
                if (!desiredVersion.Equals(actualVersion))
                {
                    DscWriter.WriteDebug($"Tool {packageId}: Version mismatch (desired: {desired.Version}, actual: {actual.Version})");
                    return false;
                }
            }
            else if (!string.Equals(desired.Version, actual.Version, StringComparison.OrdinalIgnoreCase))
            {
                DscWriter.WriteDebug($"Tool {packageId}: Version mismatch (desired: {desired.Version}, actual: {actual.Version})");
                return false;
            }
        }

        // Check commands if specified
        if (desired.Commands != null && desired.Commands.Any())
        {
            if (actual.Commands == null || !actual.Commands.Any())
            {
                DscWriter.WriteDebug($"Tool {desired.PackageId}: Commands mismatch (desired commands specified, actual: none)");
                return false;
            }

            // Check if all desired commands are present in actual
            var missingCommands = desired.Commands.Except(actual.Commands, StringComparer.OrdinalIgnoreCase).ToList();
            if (missingCommands.Any())
            {
                DscWriter.WriteDebug($"Tool {desired.PackageId}: Missing commands: {string.Join(", ", missingCommands)}");
                return false;
            }
        }

        // Check scope if specified
        if (desired.Scope.HasValue && actual.Scope.HasValue)
        {
            if (desired.Scope.Value != actual.Scope.Value)
            {
                DscWriter.WriteDebug($"Tool {desired.PackageId}: Scope mismatch (desired: {desired.Scope.Value}, actual: {actual.Scope.Value})");
                return false;
            }
        }

        if (!string.IsNullOrEmpty(desired.ToolPath) && !string.IsNullOrEmpty(actual.ToolPath))
        {
            if (!string.Equals(desired.ToolPath, actual.ToolPath, StringComparison.OrdinalIgnoreCase))
            {
                DscWriter.WriteDebug($"Tool {desired.PackageId}: ToolPath mismatch (desired: {desired.ToolPath}, actual: {actual.ToolPath})");
                return false;
            }
        }
        return true;
    }
}
