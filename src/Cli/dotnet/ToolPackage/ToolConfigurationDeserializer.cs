// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Xml;
using System.Xml.Serialization;
using Microsoft.DotNet.Cli.ToolPackage.ToolConfigurationDeserialization;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Packaging.Core;

namespace Microsoft.DotNet.Cli.ToolPackage;

internal static class ToolConfigurationDeserializer
{
    // The supported tool configuration schema version.
    // This should match the schema version in the GenerateToolsSettingsFile task from the SDK.
    private const int SupportedVersion = 2;

    public static ToolConfiguration Deserialize(string pathToXml, IFileSystem fileSystem = null)
    {
        fileSystem ??= new FileSystemWrapper();

        var serializer = new XmlSerializer(typeof(DotNetCliTool));

        DotNetCliTool dotNetCliTool;

        try
        {
            using (var stream = fileSystem.File.OpenRead(pathToXml))
            {
                var reader = XmlReader.Create(stream);
                dotNetCliTool = (DotNetCliTool)serializer.Deserialize(reader);
            }
        }
        catch (InvalidOperationException ex) when (ex.InnerException is XmlException)
        {
            throw new ToolConfigurationException(
                string.Format(
                    CliStrings.ToolSettingsInvalidXml,
                    ex.InnerException.Message),
                ex.InnerException);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            throw new ToolConfigurationException(
                string.Format(
                    CliStrings.FailedToRetrieveToolConfiguration,
                    ex.Message),
                ex);
        }

        List<string> warnings = GenerateWarningAccordingToVersionAttribute(dotNetCliTool);

        if (dotNetCliTool.Commands.Length != 1)
        {
            throw new ToolConfigurationException(CliStrings.ToolSettingsMoreThanOneCommand);
        }

        var runner = dotNetCliTool.Commands[0].Runner;
        if (!string.IsNullOrEmpty(runner) && runner != "dotnet" && runner != "executable")
        {
            throw new ToolConfigurationException(
                string.Format(
                    CliStrings.ToolSettingsUnsupportedRunner,
                    dotNetCliTool.Commands[0].Name,
                    dotNetCliTool.Commands[0].Runner));
        }

        var ridSpecificPackages = dotNetCliTool.RuntimeIdentifierPackages?.ToDictionary(p => p.RuntimeIdentifier, p => new PackageIdentity(p.Id, null))
            .AsReadOnly();

        //  Also error out if there are no RID-specific packages and the runner is empty
        if (string.IsNullOrEmpty(runner) && !ridSpecificPackages.Any())
        {
            throw new ToolConfigurationException(
                string.Format(
                    CliStrings.ToolSettingsUnsupportedRunner,
                    dotNetCliTool.Commands[0].Name,
                    dotNetCliTool.Commands[0].Runner));
        }

        return new ToolConfiguration(
            dotNetCliTool.Commands[0].Name,
            dotNetCliTool.Commands[0].EntryPoint,
            dotNetCliTool.Commands[0].Runner,
            ridSpecificPackages: ridSpecificPackages,
            warnings: warnings);
    }

    private static List<string> GenerateWarningAccordingToVersionAttribute(DotNetCliTool dotNetCliTool)
    {
        List<string> warnings = [];
        if (string.IsNullOrWhiteSpace(dotNetCliTool.Version))
        {
            warnings.Add(CliStrings.FormatVersionIsMissing);
        }
        else
        {
            if (!int.TryParse(dotNetCliTool.Version, out int version))
            {
                warnings.Add(CliStrings.FormatVersionIsMalformed);
            }
            else
            {
                if (version > SupportedVersion)
                {
                    warnings.Add(CliStrings.FormatVersionIsHigher);
                }
            }
        }

        return warnings;
    }
}
