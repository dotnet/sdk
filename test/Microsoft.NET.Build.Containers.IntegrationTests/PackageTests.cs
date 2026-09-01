// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

[TestClass]
public class PackageTests
{
    [TestMethod]
    public void SanityTest_NET_Build_ContainersDependencies()
    {
        IReadOnlyList<string> knownPackageReferences = new List<string>()
        {
            "Microsoft.Build.Utilities.Core",
            "Microsoft.CodeAnalysis.PublicApiAnalyzers",
            "Nuget.Packaging",
            "Valleysoft.DockerCredsProvider",
            "Microsoft.Extensions.Logging"
        };
        IReadOnlyList<string> knownProjectReferences = new List<string>()
        {
            "..\\..\\Cli\\Microsoft.DotNet.Cli.Utils\\Microsoft.DotNet.Cli.Utils.csproj",
            "..\\..\\Microsoft.Extensions.Logging.MSBuild\\Microsoft.Extensions.Logging.MSBuild.csproj"
        };

        string projectFilePath = Path.Combine(SdkTestContext.Current.TestExecutionDirectory, "Container", "ProjectFiles", "Microsoft.NET.Build.Containers.csproj");
        XDocument project = XDocument.Load(projectFilePath);
        XNamespace ns = project.Root?.Name.Namespace ?? throw new InvalidOperationException("Project file is empty");

        IEnumerable<string?> packageReferences = project.Descendants().Where(element => element.Name.Equals(ns + "PackageReference")).Select(element => element.Attribute("Include")?.Value);
        packageReferences.Should().BeEquivalentTo(knownPackageReferences, $"Known package references for Microsoft.NET.Build.Containers project are different from actual. Check if this is expected. If the new package reference is expected, add it to {nameof(knownPackageReferences)} and verify they are included to NuGet package in package.csproj correctly");

        IEnumerable<string?> projectReferences = project.Descendants().Where(element => element.Name.Equals(ns + "ProjectReference")).Select(element => element.Attribute("Include")?.Value);
        projectReferences.Should().BeEquivalentTo(knownProjectReferences, $"Known project references for Microsoft.NET.Build.Containers project are different from actual. Check if this is expected. If the new project reference is expected, add it to {nameof(knownProjectReferences)} and verify they are included to NuGet package in package.csproj correctly");
    }

    [TestMethod]
    public void PackageContentTest()
    {
        string ignoredZipFileEntriesPrefix = "package/services/metadata";
        var netTFM = ToolsetInfo.CurrentTargetFramework;
        IReadOnlyList<string> packageContents = new List<string>()
        {
              "_rels/.rels",
              "[Content_Types].xml",
              "build/Microsoft.NET.Build.Containers.props",
              "build/Microsoft.NET.Build.Containers.targets",
              "Icon.png",
              "Microsoft.NET.Build.Containers.nuspec",
              "README.md",
              $"tasks/{netTFM}/Microsoft.DotNet.Cli.Utils.dll",
              $"tasks/{netTFM}/Microsoft.Extensions.DependencyInjection.dll",
              $"tasks/{netTFM}/Microsoft.Extensions.Logging.dll",
              $"tasks/{netTFM}/Microsoft.Extensions.Logging.MSBuild.dll",
              $"tasks/{netTFM}/Microsoft.NET.Build.Containers.deps.json",
              $"tasks/{netTFM}/Microsoft.NET.Build.Containers.dll",
              $"tasks/{netTFM}/Newtonsoft.Json.dll",
              $"tasks/{netTFM}/NuGet.Common.dll",
              $"tasks/{netTFM}/NuGet.Configuration.dll",
              $"tasks/{netTFM}/NuGet.Frameworks.dll",
              $"tasks/{netTFM}/NuGet.Packaging.dll",
              $"tasks/{netTFM}/NuGet.Versioning.dll",
              $"tasks/{netTFM}/Valleysoft.DockerCredsProvider.dll"
        };

        (string? packageFilePath, string? packageVersion) = ToolsetUtils.GetContainersPackagePath();
        using ZipArchive archive = new(File.OpenRead(packageFilePath ?? string.Empty), ZipArchiveMode.Read, false);

        IEnumerable<string> actualEntries = archive.Entries
            .Select(e => e.FullName)
            .Where(e => !e.StartsWith(ignoredZipFileEntriesPrefix, StringComparison.InvariantCultureIgnoreCase))
            .OrderBy(e => e);

        actualEntries
                .Should()
                .BeEquivalentTo(packageContents, $"{Path.GetFileName(packageFilePath)} content differs from expected. Please add the entry to the list, if the addition is expected.");
    }
}
