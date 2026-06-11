// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using NuGet.Packaging.Core;

namespace Microsoft.DotNet.Cli.ToolPackage;

internal class ToolConfiguration
{
    public ToolConfiguration(
        string commandName,
        string toolAssemblyEntryPoint,
        string runner,
        IDictionary<string, PackageIdentity>? ridSpecificPackages = null,
        IEnumerable<string>? warnings = null)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            throw new ToolConfigurationException(CliStrings.ToolSettingsMissingCommandName);
        }

        if (string.IsNullOrWhiteSpace(toolAssemblyEntryPoint) && ridSpecificPackages?.Any() != true)
        {
            throw new ToolConfigurationException(
                string.Format(
                    CliStrings.ToolSettingsMissingEntryPoint,
                    commandName));
        }

        EnsureNoLeadingDot(commandName);
        EnsureNoInvalidFilenameCharacters(commandName);

        CommandName = commandName;
        ToolAssemblyEntryPoint = toolAssemblyEntryPoint;
        Runner = runner;
        RidSpecificPackages = ridSpecificPackages;
        Warnings = warnings ?? [];
    }

    private static void EnsureNoInvalidFilenameCharacters(string commandName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        if (commandName.IndexOfAny(invalidCharacters) != -1)
        {
            throw new ToolConfigurationException(
                string.Format(
                    CliStrings.ToolSettingsInvalidCommandName,
                    commandName,
                    string.Join(", ", invalidCharacters.Select(c => $"'{c}'"))));
        }
    }

    private static void EnsureNoLeadingDot(string commandName)
    {
        if (commandName.StartsWith(".", StringComparison.OrdinalIgnoreCase))
        {
            throw new ToolConfigurationException(
                string.Format(
                    CliStrings.ToolSettingsInvalidLeadingDotCommandName,
                    commandName));
        }
    }



    public string CommandName { get; }
    public string ToolAssemblyEntryPoint { get; }
    public string Runner { get; }

    public IDictionary<string, PackageIdentity>? RidSpecificPackages { get; }

    public IEnumerable<string> Warnings { get; }
}
