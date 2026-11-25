// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Publish.Tests
{
    [TestClass]
    public class GivenThatWeWantToFilterSatelliteAssemblies : SdkTest
    {
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void It_classifies_and_filters_project_reference_satellites(bool noBuild)
        {
            var library = new TestProject
            {
                Name = "LocalizedLibrary",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                EmbeddedResources =
                {
                    ["Strings.fr.resx"] = """<root><data name="Message"><value>Bonjour</value></data></root>""",
                    ["Strings.de.resx"] = """<root><data name="Message"><value>Hallo</value></data></root>""",
                },
            };
            var app = new TestProject
            {
                Name = "LocalizedApp",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                ReferencedProjects = { library },
                AdditionalProperties = { ["SatelliteResourceLanguages"] = "fr" },
            };
            app.ProjectChanges.Add(project => project.Root!.Add(System.Xml.Linq.XElement.Parse("""
                <Target Name="CheckReferenceSatellites" AfterTargets="ComputeResolvedFilesToPublishList">
                  <ItemGroup>
                    <_LibrarySatellites Include="@(ResolvedFileToPublish->WithMetadataValue('Filename', 'LocalizedLibrary.resources'))" />
                  </ItemGroup>
                  <Error Condition="'@(_LibrarySatellites->Count())' != '1' Or
                                    '@(_LibrarySatellites->'%(AssetType)|%(Culture)')' != 'resources|fr'"
                         Text="Expected just the French reference satellite with resource metadata." />
                </Target>
                """)));

            var asset = TestAssetsManager.CreateTestProject(app, identifier: noBuild.ToString());
            if (noBuild)
            {
                new BuildCommand(asset).Execute().Should().Pass();
            }

            var publish = new PublishCommand(asset);
            publish.Execute($"/p:NoBuild={noBuild}").Should().Pass();
            var output = publish.GetOutputDirectory(app.TargetFrameworks);
            Assert.IsTrue(File.Exists(Path.Combine(output.FullName, "fr", "LocalizedLibrary.resources.dll")));
            Assert.IsFalse(File.Exists(Path.Combine(output.FullName, "de", "LocalizedLibrary.resources.dll")));
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
