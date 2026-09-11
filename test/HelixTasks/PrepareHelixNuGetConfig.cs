// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.SdkCustomHelix.Sdk;

public sealed class PrepareHelixNuGetConfig : Build.Utilities.Task
{
    private static readonly HashSet<string> s_sourcesToRemove = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet6-transport",
        "dotnet6-internal-transport",
        "dotnet7-transport",
        "dotnet7-internal-transport",
        "dotnet8-transport",
        "dotnet8-internal-transport",
        "dotnet9-transport",
        "dotnet9-internal-transport",
        "dotnet10-transport",
        "dotnet10-internal-transport",
        "richnav",
        "vs-impl",
        "dotnet-libraries-transport",
        "dotnet-tools-transport",
        "dotnet-libraries",
        "dotnet-eng",
        "dotnet-under-test",
        "testpackages",
    };

    [Required]
    public string SourceFile { get; set; } = string.Empty;

    [Required]
    public string DestinationFile { get; set; } = string.Empty;

    public override bool Execute()
    {
        XDocument document = XDocument.Load(SourceFile, LoadOptions.PreserveWhitespace);
        XElement packageSources = document.Root?.Element("packageSources")
            ?? throw new InvalidDataException($"NuGet config '{SourceFile}' does not contain packageSources.");

        packageSources
            .Elements("add")
            .Where(source => s_sourcesToRemove.Contains((string?)source.Attribute("key") ?? string.Empty))
            .Remove();

        packageSources.Add(
            new XElement(
                "add",
                new XAttribute("key", "dotnet-under-test"),
                new XAttribute("value", "%DOTNET_ROOT%/.nuget")),
            new XElement(
                "add",
                new XAttribute("key", "testpackages"),
                new XAttribute("value", "%DOTNET_SDK_TEST_EXECUTION_DIRECTORY%/Testpackages")));

        Directory.CreateDirectory(Path.GetDirectoryName(DestinationFile)
            ?? throw new InvalidDataException($"Could not determine the destination directory for '{DestinationFile}'."));
        document.Save(DestinationFile, SaveOptions.DisableFormatting);
        return true;
    }
}
