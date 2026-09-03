// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToPackAToolProject : SdkTest
    {
        private string _testRoot;
        private string _targetFrameworkOrFrameworks = "netcoreapp2.1";

        public GivenThatWeWantToPackAToolProject(ITestOutputHelper log) : base(log)
        {
        }

        private string SetupNuGetPackage(bool multiTarget, string packageType = null, [CallerMemberName] string callingMethod = "")
        {
            string id = $"{callingMethod}-{_targetFrameworkOrFrameworks}";
            TestAsset helloWorldAsset = _testAssetsManager
                .CopyTestAsset("PortableTool", id)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    XNamespace ns = project.Root.Name.Namespace;
                    XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    if (packageType is not null)
                    {
                        propertyGroup.Add(new XElement("packageType", packageType));
                    }
                })
                .WithTargetFrameworkOrFrameworks(_targetFrameworkOrFrameworks, multiTarget);

            _testRoot = helloWorldAsset.TestRoot;

            var packCommand = new PackCommand(helloWorldAsset);

            var result = packCommand.Execute($"/bl:{id}-{{}}.binlog");
            result.Should().Pass();

            return packCommand.GetNuGetPackage();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_packs_successfully(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader
                    .GetToolItems()
                    .Should().NotBeEmpty();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_finds_the_entry_point_dll_and_command_name_and_put_in_setting_file(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            AssertFiles(nugetPackage);
        }

        [Fact]
        public void Given_nuget_alias_It_finds_the_entry_point_dll_and_command_name_and_put_in_setting_file()
        {
            TestAsset helloWorldAsset = _testAssetsManager
                .CopyTestAsset("PortableTool")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    XNamespace ns = project.Root.Name.Namespace;
                    XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Elements("TargetFramework").First().SetValue("targetframeworkAlias");
                    XElement conditionPropertyGroup = new("PropertyGroup");
                    project.Root.Add(conditionPropertyGroup);
                    conditionPropertyGroup.SetAttributeValue("Condition", "'$(TargetFramework)' == 'targetframeworkAlias'");
                    conditionPropertyGroup.SetElementValue("TargetFrameworkIdentifier", ".NETCoreApp");
                    conditionPropertyGroup.SetElementValue("TargetFrameworkVersion", "v3.1");
                    conditionPropertyGroup.SetElementValue("TargetFrameworkMoniker", ".NETCoreApp,Version=v3.1");
                });

            _testRoot = helloWorldAsset.TestRoot;

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            var result = packCommand.Execute();
            result.Should().Pass();

            var nugetPackage = packCommand.GetNuGetPackage();
            AssertFiles(nugetPackage);
        }

        private void AssertFiles(string nugetPackage)
        {
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                var anyTfm = nupkgReader.GetSupportedFrameworks().First().GetShortFolderName();
                var tmpfilePath = Path.Combine(_testRoot, "temp", Path.GetRandomFileName());
                string copiedFile = nupkgReader.ExtractFile($"tools/{anyTfm}/any/DotnetToolSettings.xml", tmpfilePath, null);

                var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                allItems.Should().Contain($"tools/{anyTfm}/any/consoledemo.runtimeconfig.json");

                XElement command = XDocument.Load(copiedFile)
                                      .Element("DotNetCliTool")
                                      .Element("Commands")
                                      .Element("Command");

                command.Attribute("Name")
                        .Value
                        .Should().Be("consoledemo", "it should contain command name that is same as the msbuild well known properties $(TargetName)");

                command.Attribute("EntryPoint")
                        .Value
                        .Should().Be("consoledemo.dll", "it should contain entry point dll that is same as the msbuild well known properties $(TargetFileName)");

            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_removes_all_package_dependencies(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader
                    .GetPackageDependencies()
                    .Should().BeEmpty();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_contains_runtimeconfig_for_each_tfm(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/consoledemo.runtimeconfig.json");
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_does_not_contain_apphost_exe(bool multiTarget)
        {
            var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
            _targetFrameworkOrFrameworks = ToolsetInfo.CurrentTargetFramework;

            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().NotContain($"tools/{framework.GetShortFolderName()}/any/consoledemo{extension}");
                }
            }

            var getValuesCommand = new GetValuesCommand(
               Log,
               _testRoot,
               _targetFrameworkOrFrameworks,
               "RunCommand",
               GetValuesCommand.ValueType.Property);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                //  If multi-targeted, we need to specify which target framework to get the value for
                string[] args = multiTarget ? new[] { $"/p:TargetFramework={_targetFrameworkOrFrameworks}" } : Array.Empty<string>();
                getValuesCommand.Execute(args)
                    .Should().Pass();
                var runCommand = new FileInfo(getValuesCommand.GetValues().Single());
                runCommand.Name
                    .Should().Be("consoledemo" + Constants.ExeSuffix, because: "The RunCommand should recognize that this is an AppHost-using project and should use the AppHost for non-tool use cases.");
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_contains_DotnetToolSettingsXml_for_each_tfm(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/DotnetToolSettings.xml");
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_does_not_contain_lib(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader.GetLibItems().Should().BeEmpty();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_contains_folder_structure_tfm_any(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader
                    .GetToolItems()
                    .Should().Contain(
                        f => f.Items.
                            Contains($"tools/{f.TargetFramework.GetShortFolderName()}/any/consoledemo.dll"));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_contains_packagetype_dotnettool(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader
                    .GetPackageTypes().Should().ContainSingle(t => t.Name == "DotnetTool");
            }
        }

        [Theory]
        [InlineData("", "DotnetTool")]
        [InlineData("MyCustomType", "DotnetTool;MyCustomType")]
        [InlineData("MyCustomType, 1.0", "DotnetTool;MyCustomType, 1.0")]
        [InlineData("dotnettool", "dotnettool")]
        [InlineData("DotnetTool, 1.0.0.0", "DotnetTool, 1.0.0.0")]
        [InlineData("DotnetTool , 1.0.0.0", "DotnetTool , 1.0.0.0")]
        [InlineData("MyDotnetTool", "DotnetTool;MyDotnetTool")]
        public void It_allows_more_package_types(string input, string expectedString)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget: false, packageType: input);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                var packageTypes = nupkgReader.GetPackageTypes();
                var expected = expectedString
                    .Split(';')
                    .Select(t => t.Split(',').Select(x => x.Trim()).ToArray())
                    .Select(t => (Name: t[0], Version: t.Length > 1 ? Version.Parse(t[1]) : PackageType.EmptyVersion))
                    .Select(t => new PackageType(t.Name, t.Version))
                    .ToList();
                packageTypes.Count.Should().Be(expected.Count);
                for (var i = 0; i < packageTypes.Count; i++)
                {
                    packageTypes[i].Name.Should().Be(expected[i].Name);
                    packageTypes[i].Version.Should().Be(expected[i].Version);
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_contains_dependencies_dll(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/Newtonsoft.Json.dll");
                }
            }
        }

        [Fact]
        public void Given_targetplatform_set_It_should_error()
        {
            TestAsset helloWorldAsset = _testAssetsManager
                .CopyTestAsset("PortableTool")
                .WithSource()
                .WithTargetFramework($"{ToolsetInfo.CurrentTargetFramework}-windows");

            _testRoot = helloWorldAsset.TestRoot;

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            var result = packCommand.Execute();
            result.Should().Fail().And.HaveStdOutContaining("NETSDK1146");

        }

        [Fact]
        public void It_packs_with_RuntimeIdentifier()
        {
            var testProject = new TestProject("ToolWithRuntimeIdentifier")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid()
            };
            testProject.AdditionalProperties["PackAsTool"] = "true";
            testProject.AdditionalProperties["ImplicitUsings"] = "enable";
            testProject.AdditionalProperties["CreateRidSpecificToolPackages"] = "false";
            testProject.AdditionalProperties["UseAppHost"] = "false";


            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var packCommand = new PackCommand(testAsset);

            packCommand.Execute().Should().Pass();

            packCommand.GetPackageDirectory().Should().HaveFile($"{testProject.Name}.1.0.0.nupkg");
            packCommand.GetPackageDirectory().Should().NotHaveFile($"{testProject.Name}.{testProject.RuntimeIdentifier}.1.0.0.nupkg");

            var nupkgPath = packCommand.GetNuGetPackage();

            using (var nupkgReader = new PackageArchiveReader(nupkgPath))
            {
                var toolSettingsItem = nupkgReader.GetToolItems().SelectMany(g => g.Items).SingleOrDefault(i => i.Equals($"tools/{testProject.TargetFrameworks}/{testProject.RuntimeIdentifier}/DotnetToolSettings.xml"));
                toolSettingsItem.Should().NotBeNull();

                var toolSettingsXml = XDocument.Load(nupkgReader.GetStream(toolSettingsItem));
                toolSettingsXml.Root.Attribute("Version").Value.Should().Be("1");
            }

        }
    }
}
