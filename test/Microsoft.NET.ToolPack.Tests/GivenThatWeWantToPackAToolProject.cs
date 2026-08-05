// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Microsoft.NET.ToolPack.Tests
{
    [TestClass]
    public class GivenThatWeWantToPackAToolProject : SdkTest
    {
        private string _testRoot;
        private string _targetFrameworkOrFrameworks = "netcoreapp2.1";
        private string SetupNuGetPackage(bool multiTarget, string packageType = null, [CallerMemberName] string callingMethod = "")
        {
            // Include all distinguishing parameters so each [DataRow] gets a unique test asset
            // directory. Tests run with method-level parallelization (MSTest.Sdk default), so rows
            // sharing a directory would race on the copied project files. Sanitize characters that
            // are unsafe for the directory name and the /bl: binlog argument below.
            string id = $"{callingMethod}-{multiTarget}-{packageType}-{_targetFrameworkOrFrameworks}"
                .Replace(' ', '_').Replace(',', '_').Replace(';', '_');
            TestAsset helloWorldAsset = TestAssetsManager
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

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
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

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void It_finds_the_entry_point_dll_and_command_name_and_put_in_setting_file(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            AssertFiles(nugetPackage);
        }

        [TestMethod]
        public void Given_nuget_alias_It_finds_the_entry_point_dll_and_command_name_and_put_in_setting_file()
        {
            TestAsset helloWorldAsset = TestAssetsManager
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

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
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

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
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

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
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

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
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

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void It_does_not_contain_lib(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader.GetLibItems().Should().BeEmpty();
            }
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
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

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void It_contains_packagetype_dotnettool(bool multiTarget)
        {
            var nugetPackage = SetupNuGetPackage(multiTarget);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader
                    .GetPackageTypes().Should().ContainSingle(t => t.Name == "DotnetTool");
            }
        }

        [TestMethod]
        [DataRow("", "DotnetTool")]
        [DataRow("MyCustomType", "DotnetTool;MyCustomType")]
        [DataRow("MyCustomType, 1.0", "DotnetTool;MyCustomType, 1.0")]
        [DataRow("dotnettool", "dotnettool")]
        [DataRow("DotnetTool, 1.0.0.0", "DotnetTool, 1.0.0.0")]
        [DataRow("DotnetTool , 1.0.0.0", "DotnetTool , 1.0.0.0")]
        [DataRow("MyDotnetTool", "DotnetTool;MyDotnetTool")]
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

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
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

        [TestMethod]
        public void Given_targetplatform_set_It_should_error()
        {
            TestAsset helloWorldAsset = TestAssetsManager
                .CopyTestAsset("PortableTool")
                .WithSource()
                .WithTargetFramework($"{ToolsetInfo.CurrentTargetFramework}-windows");

            _testRoot = helloWorldAsset.TestRoot;

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            var result = packCommand.Execute();
            result.Should().Fail().And.HaveStdOutContaining("NETSDK1146");

        }

        [TestMethod]
        public void Non_AOT_tools_can_pack_all_requested_runtime_identifiers()
        {
            ComputeToolPackageRuntimeIdentifiersToPack(
                publishAot: false,
                hostRuntimeIdentifier: "linux-x64")
                .Should().Be("win-x64;win-arm64;linux-x64;linux-arm64;osx-x64;osx-arm64");
        }

        [TestMethod]
        [DataRow("win-x64", "win-x64;win-arm64")]
        [DataRow("win-arm64", "win-arm64")]
        [DataRow("osx-x64", "osx-x64;osx-arm64")]
        [DataRow("osx-arm64", "osx-x64;osx-arm64")]
        [DataRow("linux-x64", "linux-x64")]
        [DataRow("linux-arm64", "linux-arm64")]
        public void AOT_tools_only_pack_runtime_identifiers_supported_by_the_host(
            string hostRuntimeIdentifier,
            string expectedRuntimeIdentifiers)
        {
            ComputeToolPackageRuntimeIdentifiersToPack(
                publishAot: true,
                hostRuntimeIdentifier)
                .Should().Be(expectedRuntimeIdentifiers);
        }

        [TestMethod]
        public void Predefined_items_prevent_default_tool_package_runtime_identifier_computation()
        {
            ComputeToolPackageRuntimeIdentifiersToPack(
                publishAot: true,
                hostRuntimeIdentifier: "linux-x64",
                predefinedRuntimeIdentifiersToPack: "win-arm64;osx-arm64")
                .Should().Be("win-arm64;osx-arm64");
        }

        [TestMethod]
        public void Custom_target_can_override_tool_package_runtime_identifiers_to_pack()
        {
            ComputeToolPackageRuntimeIdentifiersToPack(
                publishAot: true,
                hostRuntimeIdentifier: "linux-x64",
                customTargetRuntimeIdentifiersToPack: "win-arm64;osx-arm64")
                .Should().Be("win-arm64;osx-arm64");
        }

        private string ComputeToolPackageRuntimeIdentifiersToPack(
            bool publishAot,
            string hostRuntimeIdentifier,
            string predefinedRuntimeIdentifiersToPack = null,
            string customTargetRuntimeIdentifiersToPack = null,
            [CallerMemberName] string callingMethod = "")
        {
            TestAsset testAsset = TestAssetsManager
                .CopyTestAsset("PortableTool", $"{callingMethod}-{hostRuntimeIdentifier}")
                .WithSource();

            testAsset.WithProjectChanges(project =>
            {
                XNamespace ns = project.Root.Name.Namespace;
                XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                propertyGroup.SetElementValue(
                    ns + "ToolPackageRuntimeIdentifiers",
                    "win-x64;win-arm64;linux-x64;linux-arm64;osx-x64;osx-arm64");

                if (predefinedRuntimeIdentifiersToPack is not null)
                {
                    project.Root.Add(
                        new XElement(
                            ns + "ItemGroup",
                            new XElement(
                                ns + "ToolPackageRuntimeIdentifiersToPack",
                                new XAttribute("Include", predefinedRuntimeIdentifiersToPack))));
                }

                if (customTargetRuntimeIdentifiersToPack is not null)
                {
                    project.Root.Add(
                        new XElement(
                            ns + "Target",
                            new XAttribute("Name", "OverrideToolPackageRuntimeIdentifiersToPack"),
                            new XAttribute("BeforeTargets", "ComputeToolPackageRuntimeIdentifiersToPack"),
                            new XElement(
                                ns + "ItemGroup",
                                new XElement(
                                    ns + "ToolPackageRuntimeIdentifiersToPack",
                                    new XAttribute("Include", customTargetRuntimeIdentifiersToPack)))));
                }
            });

            List<string> arguments =
            [
                $"/p:PublishAot={publishAot}",
                $"/p:NETCoreSdkPortableRuntimeIdentifier={hostRuntimeIdentifier}",
                "/p:RuntimeIdentifier=",
                "-getItem:ToolPackageRuntimeIdentifiersToPack",
                $"/bl:{callingMethod}-{hostRuntimeIdentifier}.binlog",
            ];

            CommandResult result = new MSBuildCommand(
                testAsset,
                "ComputeToolPackageRuntimeIdentifiersToPack;_ComputeDefaultToolPackageRuntimeIdentifiersToPack")
                .ExecuteWithoutRestore([.. arguments]);
            result.Should().Pass();
            return string.Join(
                ";",
                JObject.Parse(result.StdOut)["Items"]["ToolPackageRuntimeIdentifiersToPack"]
                    .Select(item => item["Identity"].Value<string>()));
        }

        [TestMethod]
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


            var testAsset = TestAssetsManager.CreateTestProject(testProject);

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

        [TestMethod]
        public void Framework_dependent_tool_should_target_base_runtime_version()
        {
            // This test verifies that framework-dependent tools (FDD) correctly target the .0 patch version
            // instead of a specific patch version, ensuring compatibility across runtime installations.
            // File-based apps default to PublishAot=true, which was causing the issue, so we set it here
            // to properly test the fix.
            var testProject = new TestProject()
            {
                Name = "FddToolTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                IsSdkProject = true,
            };

            testProject.AdditionalProperties["PackAsTool"] = "true";
            testProject.AdditionalProperties["ToolCommandName"] = "fddtool";
            testProject.AdditionalProperties["PublishAot"] = "true";

            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: "FddToolRuntimeVersion");

            var packCommand = new PackCommand(testAsset);
            packCommand.Execute().Should().Pass();

            var nupkgPath = packCommand.GetNuGetPackage();

            using (var nupkgReader = new PackageArchiveReader(nupkgPath))
            {
                var anyTfm = nupkgReader.GetSupportedFrameworks().First().GetShortFolderName();
                var runtimeConfigPath = $"tools/{anyTfm}/any/{testProject.Name}.runtimeconfig.json";

                // Read the runtimeconfig.json directly from the archive
                using (var stream = nupkgReader.GetStream(runtimeConfigPath))
                using (var reader = new StreamReader(stream))
                {
                    string runtimeConfigContents = reader.ReadToEnd();
                    var runtimeConfig = JObject.Parse(runtimeConfigContents);

                    // Get the framework version
                    var frameworkVersion = runtimeConfig["runtimeOptions"]["framework"]["version"].Value<string>();

                    // Parse the version to get the base version (major.minor.patch) without any prerelease suffix
                    // e.g., "11.0.0-preview.1.26069.105" -> "11.0.0"
                    var dashIndex = frameworkVersion.IndexOf('-');
                    var baseVersion = dashIndex >= 0 ? frameworkVersion.Substring(0, dashIndex) : frameworkVersion;

                    // Verify it matches the expected pattern (major.minor.0)
                    var versionParts = baseVersion.Split('.');
                    versionParts.Should().HaveCount(3, because: "version should be in format major.minor.patch");
                    versionParts[2].Should().Be("0", because: "patch version should be 0 for FDD tools to ensure compatibility across runtime installations");
                }
            }
        }
    }
}
