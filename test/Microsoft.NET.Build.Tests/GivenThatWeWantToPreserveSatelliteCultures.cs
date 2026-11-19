// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.NET.Build.Tests;

[TestClass]
public class GivenThatWeWantToPreserveSatelliteCultures : SdkTest
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(false)]
    [DataRow(true)]
    public void It_loads_package_satellites_under_ICU_after_building_with_NLS(bool publish)
    {
        string[] cultures = ["ckb", "ckb-IQ", "de"];
        var packageProject = new TestProject
        {
            Name = "SatelliteCultures",
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            SourceFiles =
            {
                ["Marker.cs"] = "namespace SatelliteCultures; public class Marker { }"
            },
            EmbeddedResources =
            {
                ["Strings.resx"] = """
                    <root>
                      <data name="Greeting" xml:space="preserve"><value>Neutral fallback</value></data>
                    </root>
                    """
            }
        };
        foreach (string culture in cultures)
        {
            packageProject.EmbeddedResources[$"Strings.{culture}.resx"] = $"""
                <root>
                  <data name="Greeting" xml:space="preserve"><value>Resource for {culture}</value></data>
                </root>
                """;
        }

        var packageAsset = TestAssetsManager.CreateTestProject(packageProject, identifier: publish.ToString());
        string packageDirectory = Path.Combine(packageAsset.TestRoot, "feed");
        string packageProjectPath = Path.Combine(packageAsset.TestRoot, packageProject.Name, "SatelliteCultures.csproj");

        // Produce the package under ICU even when the consumer is tested with full-framework MSBuild.
        new DotnetPackCommand(Log, packageProjectPath, "-c", "Debug", "-o", packageDirectory,
            "/p:UseSharedCompilation=false", "/nr:false")
            .WithEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_USENLS", "0")
            .WithEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "0")
            .Execute()
            .Should().Pass();

        var appProject = new TestProject
        {
            Name = "SatelliteConsumer",
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            IsExe = true,
            SourceFiles =
            {
                ["Program.cs"] = """
                    using System;
                    using System.Globalization;
                    using System.Resources;
                    using SatelliteCultures;

                    var resources = new ResourceManager("SatelliteCultures.Strings", typeof(Marker).Assembly);
                    foreach (string name in new[] { "ckb", "ckb-IQ", "de" })
                    {
                        var culture = CultureInfo.GetCultureInfo(name);
                        if (culture.Name != name)
                            throw new Exception($"Expected ICU culture {name}, got {culture.Name}.");

                        var resourceSet = resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
                        string actual = resourceSet?.GetString("Greeting");
                        string expected = $"Resource for {name}";
                        if (actual != expected)
                            throw new Exception($"Expected '{expected}', got '{actual}'.");
                        Console.WriteLine(actual);
                    }
                    """
            }
        };
        appProject.PackageReferences.Add(new TestPackageReference(
            packageProject.Name, "1.0.0", Path.Combine(packageDirectory, "SatelliteCultures.1.0.0.nupkg")));
        appProject.AdditionalProperties["RestoreAdditionalProjectSources"] = packageDirectory;
        appProject.AdditionalProperties["RestorePackagesPath"] = "$(MSBuildProjectDirectory)/packages";
        appProject.ProjectChanges.Add(project =>
        {
            var root = project.Root!;
            root.Add(XElement.Parse("""
                <Target Name="CheckNlsCultureNames" BeforeTargets="ResolvePackageAssets">
                  <PropertyGroup>
                    <CkbCultureName>$([System.Globalization.CultureInfo]::GetCultureInfo('ckb').Name)</CkbCultureName>
                    <CkbIqCultureName>$([System.Globalization.CultureInfo]::GetCultureInfo('ckb-IQ').Name)</CkbIqCultureName>
                  </PropertyGroup>
                  <Error Condition="'$(CkbCultureName)' != 'ku'"
                         Text="This regression test requires an NLS build host that remaps ckb to ku." />
                  <Error Condition="'$(CkbIqCultureName)' != 'ku-Arab-IQ'"
                         Text="This regression test requires an NLS build host that remaps ckb-IQ to ku-Arab-IQ." />
                </Target>
                """));
            root.Add(XElement.Parse("""
                <Target Name="RecordResourceCultures" AfterTargets="ResolvePackageAssets">
                  <WriteLinesToFile File="$(IntermediateOutputPath)resource-cultures.txt"
                                    Lines="@(ResourceCopyLocalItems->'%(Culture)|%(DestinationSubDirectory)|%(DestinationSubPath)')"
                                    Overwrite="true" />
                </Target>
                """));
        });

        var appAsset = TestAssetsManager.CreateTestProject(appProject, identifier: publish.ToString());
        MSBuildCommand command = publish ? new PublishCommand(appAsset) : new BuildCommand(appAsset);
        var buildResult = command
            .WithEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_USENLS", "1")
            .WithEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "0")
            .Execute("/p:UseSharedCompilation=false", "/nr:false");
        buildResult.Should().Pass();

        string metadataPath = Path.Combine(
            command.GetIntermediateDirectory(appProject.TargetFrameworks).FullName, "resource-cultures.txt");
        File.ReadAllLines(metadataPath).Should().BeEquivalentTo(cultures.Select(culture =>
            $"{culture}|{culture}{Path.DirectorySeparatorChar}|{Path.Combine(culture, "SatelliteCultures.resources.dll")}"));
        buildResult.Should().NotHaveStdOutContaining("NETSDK1187")
            .And.NotHaveStdOutContaining("NETSDK1188");

        string outputDirectory = command.GetOutputDirectory(appProject.TargetFrameworks).FullName;
        foreach (string culture in cultures)
        {
            File.Exists(Path.Combine(outputDirectory, culture, "SatelliteCultures.resources.dll")).Should().BeTrue();
        }
        Directory.Exists(Path.Combine(outputDirectory, "ku")).Should().BeFalse();
        Directory.Exists(Path.Combine(outputDirectory, "ku-Arab-IQ")).Should().BeFalse();

        using var deps = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDirectory, "SatelliteConsumer.deps.json")));
        var resourceEntries = deps.RootElement.GetProperty("targets").EnumerateObject().Single().Value
            .GetProperty("SatelliteCultures/1.0.0").GetProperty("resources");
        resourceEntries.EnumerateObject()
            .Select(resource => (resource.Name, Locale: resource.Value.GetProperty("locale").GetString()))
            .Should().BeEquivalentTo(cultures.Select(culture =>
                ($"lib/{packageProject.TargetFrameworks}/{culture}/SatelliteCultures.resources.dll", culture)));

        var runResult = new DotnetCommand(Log, Path.Combine(outputDirectory, "SatelliteConsumer.dll"))
            .WithEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_USENLS", "0")
            .WithEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "0")
            .Execute();
        runResult.Should().Pass();
        foreach (string culture in cultures)
        {
            runResult.Should().HaveStdOutContaining($"Resource for {culture}");
        }
    }
}
