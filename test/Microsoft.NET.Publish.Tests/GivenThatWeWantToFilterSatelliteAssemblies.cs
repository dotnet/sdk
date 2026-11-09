// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Publish.Tests
{
    [TestClass]
    public class GivenThatWeWantToFilterSatelliteAssemblies : SdkTest
    {
        [TestMethod]
        [DataRow(false, false)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        public void It_identifies_project_satellites_as_resource_assets(bool noBuild, bool native)
        {
            TestProject CreateProject(string name) => new()
            {
                Name = name,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                EmbeddedResources =
                {
                    ["Strings.resx"] = """
                        <root><data name="Greeting" xml:space="preserve"><value>Neutral</value></data></root>
                        """,
                    ["Strings.de.resx"] = """
                        <root><data name="Greeting" xml:space="preserve"><value>German</value></data></root>
                        """,
                    ["Strings.zh-Hans.resx"] = """
                        <root><data name="Greeting" xml:space="preserve"><value>Chinese</value></data></root>
                        """
                }
            };

            var transitive = CreateProject("TransitiveResources");
            transitive.SourceFiles["Marker.cs"] = "namespace TransitiveResources; public class Marker { }";
            var library = CreateProject("LibraryResources");
            library.SourceFiles["Marker.cs"] = "namespace LibraryResources; public class Marker { }";
            library.ReferencedProjects.Add(transitive);
            var app = CreateProject("ResourceConsumer");
            app.IsExe = true;
            app.ReferencedProjects.Add(library);
            app.SourceFiles["Program.cs"] = """
                using System;
                using System.Globalization;
                using System.Reflection;
                using System.Resources;

                foreach ((string name, Assembly assembly) in new[]
                {
                    ("ResourceConsumer", typeof(Program).Assembly),
                    ("LibraryResources", typeof(LibraryResources.Marker).Assembly),
                    ("TransitiveResources", typeof(TransitiveResources.Marker).Assembly)
                })
                {
                    var resources = new ResourceManager(name + ".Strings", assembly);
                    foreach (string culture in new[] { "de-DE", "zh-CN", "it-IT" })
                    {
                        Console.WriteLine($"{name}:{culture}:{resources.GetString("Greeting", CultureInfo.GetCultureInfo(culture))}");
                    }
                }
                """;
            if (native)
            {
                app.AdditionalProperties["PublishAot"] = "true";
                app.RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier;
            }
            app.ProjectChanges.Add(project => project.Root!.Add(XElement.Parse("""
                <Target Name="RecordPublishAssets" AfterTargets="ComputeResolvedFilesToPublishList">
                  <WriteLinesToFile File="$(OutputPath)publish-assets.txt"
                                    Lines="@(ResolvedFileToPublish->'%(RelativePath)|%(AssetType)')"
                                    Overwrite="true" />
                </Target>
                """)));

            var asset = TestAssetsManager.CreateTestProject(app, identifier: $"{noBuild}_{native}");
            if (noBuild)
            {
                new BuildCommand(asset).Execute().Should().Pass();
            }

            var publish = new PublishCommand(asset);
            publish.Execute($"/p:NoBuild={noBuild}").Should().Pass();

            string[] assets = File.ReadAllLines(Path.Combine(
                new BuildCommand(asset).GetOutputDirectory(app.TargetFrameworks, runtimeIdentifier: app.RuntimeIdentifier).FullName, "publish-assets.txt"));
            foreach (string name in new[] { "ResourceConsumer", "LibraryResources", "TransitiveResources" })
            {
                foreach (string culture in new[] { "de", "zh-Hans" })
                {
                    assets.Should().Contain($"{Path.Combine(culture, name + ".resources.dll")}|resources");
                }
                assets.Should().Contain($"{name}.dll|");
            }

            if (native)
            {
                string output = publish.GetOutputDirectory(app.TargetFrameworks, runtimeIdentifier: app.RuntimeIdentifier).FullName;
                Directory.GetFiles(output, "*.resources.dll", SearchOption.AllDirectories).Should().BeEmpty();
                var result = new RunExeCommand(Log, Path.Combine(output, app.Name + Constants.ExeSuffix)).Execute();
                result.Should().Pass();
                foreach (string name in new[] { "ResourceConsumer", "LibraryResources", "TransitiveResources" })
                {
                    result.StdOut.Should().Contain($"{name}:de-DE:German")
                        .And.Contain($"{name}:zh-CN:Chinese")
                        .And.Contain($"{name}:it-IT:Neutral");
                }
            }
        }

        [TestMethod]
        [DataRow("netcoreapp2.0")]
        [DataRow(ToolsetInfo.CurrentTargetFramework)]

        public void It_only_publishes_selected_ResourceLanguages(string tfm)
        {
            var testProject = new TestProject()
            {
                Name = "PublishFilteredSatelliteAssemblies",
                TargetFrameworks = tfm,
                IsExe = true,
            };

            testProject.PackageReferences.Add(new TestPackageReference("System.Spatial", "5.8.3"));
            testProject.AdditionalProperties.Add("SatelliteResourceLanguages", "en-US;it;fr");

            var testProjectInstance = TestAssetsManager.CreateTestProject(testProject, identifier: tfm);

            var publishCommand = new PublishCommand(testProjectInstance);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: testProject.TargetFrameworks);

            var files = new List<string>() {
                "it/System.Spatial.resources.dll",
                "fr/System.Spatial.resources.dll",
                "System.Spatial.dll",
                $"{testProject.Name}.dll",
                $"{testProject.Name}.pdb",
                $"{testProject.Name}.deps.json",
                $"{testProject.Name}.runtimeconfig.json"
            };

            if (tfm == ToolsetInfo.CurrentTargetFramework)
            {
                files.Add($"{testProject.Name}{Constants.ExeSuffix}");
            }

            publishDirectory.Should().OnlyHaveFiles(files);
        }
        [TestMethod]
        public void It_publishes_all_satellites_when_not_filtered()
        {
            var testProject = new TestProject()
            {
                Name = "PublishSatelliteAssemblies",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            testProject.PackageReferences.Add(new TestPackageReference("System.Spatial", "5.8.3"));

            var testProjectInstance = TestAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testProjectInstance);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: testProject.TargetFrameworks);

            publishDirectory.Should().OnlyHaveFiles(new[] {
                "de/System.Spatial.resources.dll",
                "es/System.Spatial.resources.dll",
                "fr/System.Spatial.resources.dll",
                "it/System.Spatial.resources.dll",
                "ja/System.Spatial.resources.dll",
                "ko/System.Spatial.resources.dll",
                "ru/System.Spatial.resources.dll",
                "zh-Hans/System.Spatial.resources.dll",
                "zh-Hant/System.Spatial.resources.dll",
                "System.Spatial.dll",
                $"{testProject.Name}.dll",
                $"{testProject.Name}.pdb",
                $"{testProject.Name}.deps.json",
                $"{testProject.Name}.runtimeconfig.json",
                $"{testProject.Name}{EnvironmentInfo.ExecutableExtension}"
            });
        }
    }
}
