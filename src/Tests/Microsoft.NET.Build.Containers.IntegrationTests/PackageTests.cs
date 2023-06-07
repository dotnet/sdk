﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO.Compression;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.DotNet.Cli.Utils;
using Xunit;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class PackageTests
{
    [Fact]
    public void SanityTest_ContainerizeDependencies()
    {
        IReadOnlyList<string> knownPackageReferences = new List<string>()
        {
            "System.CommandLine"
        };
        IReadOnlyList<string> knownProjectReferences = new List<string>()
        {
            "..\\Microsoft.NET.Build.Containers\\Microsoft.NET.Build.Containers.csproj"
        };

        string projectFilePath = Path.Combine(TestContext.Current.TestExecutionDirectory, "Container", "ProjectFiles", "containerize.csproj");
        XDocument project = XDocument.Load(projectFilePath);
        XNamespace ns = project.Root?.Name.Namespace ?? throw new InvalidOperationException("Project file is empty");

        IEnumerable<string?> packageReferences = project.Descendants().Where(element => element.Name.Equals(ns + "PackageReference")).Select(element => element.Attribute("Include")?.Value);
        packageReferences.Should().BeEquivalentTo(knownPackageReferences, $"Known package references for containerize project are different from actual. Check if this is expected. If the new package reference is expected, add it to {nameof(knownPackageReferences)} and verify they are included to NuGet package in package.csproj correctly");

        IEnumerable<string?> projectReferences = project.Descendants().Where(element => element.Name.Equals(ns + "ProjectReference")).Select(element => element.Attribute("Include")?.Value);
        projectReferences.Should().BeEquivalentTo(knownProjectReferences, $"Known project references for containerize project are different from actual. Check if this is expected. If the new project reference is expected, add it to {nameof(knownProjectReferences)} and verify they are included to NuGet package in package.csproj correctly");
    }

    [Fact]
    public void SanityTest_NET_Build_ContainersDependencies()
    {
        IReadOnlyList<string> knownPackageReferences = new List<string>()
        {
            "Microsoft.Build.Utilities.Core",
            "Microsoft.CodeAnalysis.PublicApiAnalyzers",
            "Nuget.Packaging",
            "Valleysoft.DockerCredsProvider"
        };
        IReadOnlyList<string> knownProjectReferences = new List<string>()
        {
            "..\\..\\Cli\\Microsoft.DotNet.Cli.Utils\\Microsoft.DotNet.Cli.Utils.csproj"
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

        IReadOnlyList<string> packageContents = new List<string>()
        {
              "_rels/.rels",
              "[Content_Types].xml",
              "build/Microsoft.NET.Build.Containers.props",
              "build/Microsoft.NET.Build.Containers.targets",
              "containerize/containerize.dll",
              "containerize/containerize.runtimeconfig.json",
              "containerize/Microsoft.DotNet.Cli.Utils.dll",
              "containerize/Microsoft.NET.Build.Containers.dll",
              "containerize/Newtonsoft.Json.dll",
              "containerize/NuGet.Common.dll",
              "containerize/NuGet.Configuration.dll",
              "containerize/NuGet.DependencyResolver.Core.dll",
              "containerize/NuGet.Frameworks.dll",
              "containerize/NuGet.LibraryModel.dll",
              "containerize/NuGet.Packaging.dll",
              "containerize/NuGet.ProjectModel.dll",
              "containerize/NuGet.Protocol.dll",
              "containerize/NuGet.Versioning.dll",
              "containerize/System.CommandLine.dll",
              "containerize/Valleysoft.DockerCredsProvider.dll",
              "Icon.png",
              "Microsoft.NET.Build.Containers.nuspec",
              "README.md",
              "tasks/net472/Microsoft.NET.Build.Containers.dll",
              "tasks/net472/Newtonsoft.Json.dll",
              "tasks/net472/NuGet.Common.dll",
              "tasks/net472/NuGet.Configuration.dll",
              "tasks/net472/NuGet.DependencyResolver.Core.dll",
              "tasks/net472/NuGet.Frameworks.dll",
              "tasks/net472/NuGet.LibraryModel.dll",
              "tasks/net472/NuGet.Packaging.Core.dll",
              "tasks/net472/NuGet.Packaging.dll",
              "tasks/net472/NuGet.ProjectModel.dll",
              "tasks/net472/NuGet.Protocol.dll",
              "tasks/net472/NuGet.Versioning.dll",
              "tasks/net8.0/Microsoft.DotNet.Cli.Utils.dll",
              "tasks/net8.0/Microsoft.NET.Build.Containers.deps.json",
              "tasks/net8.0/Microsoft.NET.Build.Containers.dll",
              "tasks/net8.0/Newtonsoft.Json.dll",
              "tasks/net8.0/NuGet.Common.dll",
              "tasks/net8.0/NuGet.Configuration.dll",
              "tasks/net8.0/NuGet.DependencyResolver.Core.dll",
              "tasks/net8.0/NuGet.Frameworks.dll",
              "tasks/net8.0/NuGet.LibraryModel.dll",
              "tasks/net8.0/NuGet.Packaging.dll",
              "tasks/net8.0/NuGet.Packaging.Core.dll",
              "tasks/net8.0/NuGet.ProjectModel.dll",
              "tasks/net8.0/NuGet.Protocol.dll",
              "tasks/net8.0/NuGet.Versioning.dll",
              "tasks/net8.0/Valleysoft.DockerCredsProvider.dll"
        };

        string packageFilePath = Path.Combine(TestContext.Current.TestExecutionDirectory, "Container", "package", $"Microsoft.NET.Build.Containers.{Product.Version}.nupkg");
        using ZipArchive archive = new(File.OpenRead(packageFilePath), ZipArchiveMode.Read, false);

        archive.Entries
                .Select(e => e.FullName)
                .Where(e => !e.StartsWith(ignoredZipFileEntriesPrefix, StringComparison.InvariantCultureIgnoreCase))
                .Should()
                .BeEquivalentTo(packageContents, $"Microsoft.NET.Build.Containers.{Product.Version}.nupkg content differs from expected. Please add the entry to the list, if the addition is expected.");
    }
}
