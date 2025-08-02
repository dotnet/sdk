// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO.Compression;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class PackageTests
{
    [Fact]
    public void SanityTest_CreateLayerTarballDependencies()
    {
        IReadOnlyList<string> knownPackageReferences = new List<string>()
        {
            "System.CommandLine",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Logging.Console"
        };
        IReadOnlyList<string> knownProjectReferences = new List<string>()
        {
            "..\\Microsoft.NET.Build.Containers\\Microsoft.NET.Build.Containers.csproj",
            "..\\..\\Cli\\Microsoft.DotNet.Cli.Utils\\Microsoft.DotNet.Cli.Utils.csproj"
        };

        string projectFilePath = Path.Combine(TestContext.Current.TestExecutionDirectory, "Container", "ProjectFiles", "CreateLayerTarball.csproj");
        XDocument project = XDocument.Load(projectFilePath);
        XNamespace ns = project.Root?.Name.Namespace ?? throw new InvalidOperationException("Project file is empty");

        IEnumerable<string?> packageReferences = project.Descendants().Where(element => element.Name.Equals(ns + "PackageReference")).Select(element => element.Attribute("Include")?.Value);
        packageReferences.Should().BeEquivalentTo(knownPackageReferences, $"Known package references for CreateLayerTarball project are different from actual. Check if this is expected. If the new package reference is expected, add it to {nameof(knownPackageReferences)} and verify they are included to NuGet package in package.csproj correctly");

        IEnumerable<string?> projectReferences = project.Descendants().Where(element => element.Name.Equals(ns + "ProjectReference")).Select(element => element.Attribute("Include")?.Value);
        projectReferences.Should().BeEquivalentTo(knownProjectReferences, $"Known project references for CreateLayerTarball project are different from actual. Check if this is expected. If the new project reference is expected, add it to {nameof(knownProjectReferences)} and verify they are included to NuGet package in package.csproj correctly");
    }

    [Fact]
    public void SanityTest_NET_Build_ContainersDependencies()
    {
        IReadOnlyList<string> knownPackageReferences = new List<string>()
        {
            "Microsoft.Build.Utilities.Core",
            "Microsoft.CodeAnalysis.PublicApiAnalyzers",
            "Nuget.Packaging",
            "System.Text.Json",
            "Valleysoft.DockerCredsProvider",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Logging.Abstractions"
        };
        IReadOnlyList<string> knownProjectReferences = new List<string>()
        {
            "..\\..\\Cli\\Microsoft.DotNet.Cli.Utils\\Microsoft.DotNet.Cli.Utils.csproj",
            "..\\..\\Microsoft.Extensions.Logging.MSBuild\\Microsoft.Extensions.Logging.MSBuild.csproj"
        };

        string projectFilePath = Path.Combine(TestContext.Current.TestExecutionDirectory, "Container", "ProjectFiles", "Microsoft.NET.Build.Containers.csproj");
        XDocument project = XDocument.Load(projectFilePath);
        XNamespace ns = project.Root?.Name.Namespace ?? throw new InvalidOperationException("Project file is empty");

        IEnumerable<string?> packageReferences = project.Descendants().Where(element => element.Name.Equals(ns + "PackageReference")).Select(element => element.Attribute("Include")?.Value);
        packageReferences.Should().BeEquivalentTo(knownPackageReferences, $"Known package references for Microsoft.NET.Build.Containers project are different from actual. Check if this is expected. If the new package reference is expected, add it to {nameof(knownPackageReferences)} and verify they are included to NuGet package in package.csproj correctly");

        IEnumerable<string?> projectReferences = project.Descendants().Where(element => element.Name.Equals(ns + "ProjectReference")).Select(element => element.Attribute("Include")?.Value);
        projectReferences.Should().BeEquivalentTo(knownProjectReferences, $"Known project references for Microsoft.NET.Build.Containers project are different from actual. Check if this is expected. If the new project reference is expected, add it to {nameof(knownProjectReferences)} and verify they are included to NuGet package in package.csproj correctly");
    }

    [Fact]
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
              "CreateLayerTarball/CreateLayerTarball.dll",
              "CreateLayerTarball/CreateLayerTarball.runtimeconfig.json",
              "CreateLayerTarball/Microsoft.DotNet.Cli.Utils.dll",
              "CreateLayerTarball/Microsoft.Extensions.Configuration.Abstractions.dll",
              "CreateLayerTarball/Microsoft.Extensions.Configuration.Binder.dll",
              "CreateLayerTarball/Microsoft.Extensions.Configuration.dll",
              "CreateLayerTarball/Microsoft.Extensions.DependencyInjection.Abstractions.dll",
              "CreateLayerTarball/Microsoft.Extensions.DependencyInjection.dll",
              "CreateLayerTarball/Microsoft.Extensions.DependencyModel.dll",
              "CreateLayerTarball/Microsoft.Extensions.Logging.Abstractions.dll",
              "CreateLayerTarball/Microsoft.Extensions.Logging.Configuration.dll",
              "CreateLayerTarball/Microsoft.Extensions.Logging.Console.dll",
              "CreateLayerTarball/Microsoft.Extensions.Logging.MSBuild.dll",
              "CreateLayerTarball/Microsoft.Extensions.Logging.dll",
              "CreateLayerTarball/Microsoft.Extensions.Options.ConfigurationExtensions.dll",
              "CreateLayerTarball/Microsoft.Extensions.Options.dll",
              "CreateLayerTarball/Microsoft.Extensions.Primitives.dll",
              "CreateLayerTarball/Microsoft.NET.Build.Containers.dll",
              "CreateLayerTarball/Newtonsoft.Json.dll",
              "CreateLayerTarball/NuGet.Common.dll",
              "CreateLayerTarball/NuGet.Configuration.dll",
              "CreateLayerTarball/NuGet.DependencyResolver.Core.dll",
              "CreateLayerTarball/NuGet.Frameworks.dll",
              "CreateLayerTarball/NuGet.LibraryModel.dll",
              "CreateLayerTarball/NuGet.Packaging.dll",
              "CreateLayerTarball/NuGet.ProjectModel.dll",
              "CreateLayerTarball/NuGet.Protocol.dll",
              "CreateLayerTarball/NuGet.Versioning.dll",
              "CreateLayerTarball/System.CommandLine.dll",
              "CreateLayerTarball/Valleysoft.DockerCredsProvider.dll",
              "Icon.png",
              "Microsoft.NET.Build.Containers.nuspec",
              "README.md",
              "tasks/net472/Microsoft.NET.Build.Containers.dll",
              "tasks/net472/Newtonsoft.Json.dll",
              "tasks/net472/NuGet.Common.dll",
              "tasks/net472/NuGet.Configuration.dll",
              "tasks/net472/NuGet.Frameworks.dll",
              "tasks/net472/NuGet.Packaging.dll",
              "tasks/net472/NuGet.Versioning.dll",
              $"tasks/{netTFM}/Microsoft.DotNet.Cli.Utils.dll",
              $"tasks/{netTFM}/Microsoft.Extensions.DependencyInjection.Abstractions.dll",
              $"tasks/{netTFM}/Microsoft.Extensions.DependencyInjection.dll",
              $"tasks/{netTFM}/Microsoft.Extensions.Logging.Abstractions.dll",
              $"tasks/{netTFM}/Microsoft.Extensions.Logging.dll",
              $"tasks/{netTFM}/Microsoft.Extensions.Logging.MSBuild.dll",
              $"tasks/{netTFM}/Microsoft.Extensions.Options.dll",
              $"tasks/{netTFM}/Microsoft.Extensions.Primitives.dll",
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
