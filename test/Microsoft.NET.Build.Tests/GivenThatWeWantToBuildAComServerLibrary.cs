// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Newtonsoft.Json.Linq;

namespace Microsoft.NET.Build.Tests
{
    [TestClass]
    public class GivenThatWeWantToBuildAComServerLibrary : SdkTest
    {

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void It_copies_the_comhost_to_the_output_directory()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("ComServer")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "ComServer.dll",
                "ComServer.pdb",
                "ComServer.deps.json",
                "ComServer.comhost.dll",
                "ComServer.runtimeconfig.json",
                "ComServer.runtimeconfig.dev.json"
            });

            string runtimeConfigFile = Path.Combine(outputDirectory.FullName, "ComServer.runtimeconfig.json");
            string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);
            JObject runtimeConfig = JObject.Parse(runtimeConfigContents);
            runtimeConfig["runtimeOptions"]["rollForward"].Value<string>()
                .Should().Be("LatestMinor");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void It_generates_a_regfree_com_manifest_when_requested()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("ComServer")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement("EnableRegFreeCom", true));
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "ComServer.dll",
                "ComServer.pdb",
                "ComServer.deps.json",
                "ComServer.comhost.dll",
                "ComServer.X.manifest",
                "ComServer.runtimeconfig.json",
                "ComServer.runtimeconfig.dev.json"
            });
        }

        [TestMethod]
        [DataRow($"{ToolsetInfo.LatestWinRuntimeIdentifier}-x64")]
        [DataRow($"{ToolsetInfo.LatestWinRuntimeIdentifier}-x86")]
        public void It_embeds_the_clsidmap_in_the_comhost_when_rid_specified(string rid)
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("ComServer", rid)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement("RuntimeIdentifier", rid));
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework, runtimeIdentifier: rid);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "ComServer.dll",
                "ComServer.pdb",
                "ComServer.deps.json",
                "ComServer.comhost.dll",
                "ComServer.runtimeconfig.json",
                "ComServer.runtimeconfig.dev.json"
            });
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void It_warns_on_self_contained_build()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("ComServer")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement("SelfContained", true));
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("NETSDK1128: ");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
        public void It_fails_to_find_comhost_for_platforms_without_comhost()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("ComServer")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1091: ");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void It_embeds_single_typelib_with_default_id()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("ComServerWithTypeLibs")
                .WithSource()
                .WithProjectChanges(proj => proj.Root.Add(new XElement("ItemGroup", new XElement("ComHostTypeLibrary", new XAttribute("Include", "dummy1.tlb")))));

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void It_fails_when_multiple_typelibs_without_ids_specified()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("ComServerWithTypeLibs")
                .WithSource()
                .WithProjectChanges(proj =>
                    proj.Root.Add(
                        new XElement("ItemGroup",
                            new XElement("ComHostTypeLibrary", new XAttribute("Include", "dummy1.tlb")),
                            new XElement("ComHostTypeLibrary", new XAttribute("Include", "dummy2.tlb")))));

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1171: ");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void It_fails_when_multiple_typelibs_with_same_ids_specified()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("ComServerWithTypeLibs")
                .WithSource()
                .WithProjectChanges(proj =>
                    proj.Root.Add(
                        new XElement("ItemGroup",
                            new XElement("ComHostTypeLibrary", new XAttribute("Include", "dummy1.tlb"), new XAttribute("Id", 1)),
                            new XElement("ComHostTypeLibrary", new XAttribute("Include", "dummy2.tlb"), new XAttribute("Id", 1)))));

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1169: ");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        [DataRow("non-integer-id")]
        [DataRow(ushort.MaxValue + 1)]
        [DataRow(0)]
        [DataRow(3.14)]
        public void It_fails_when_typelib_with_invalid_id_specified(object id)
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("ComServerWithTypeLibs", identifier: id.ToString())
                .WithSource()
                .WithProjectChanges(proj =>
                    proj.Root.Add(
                        new XElement("ItemGroup",
                            new XElement("ComHostTypeLibrary", new XAttribute("Include", "dummy1.tlb"), new XAttribute("Id", id)))));

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1170: ");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void It_embeds_multiple_typelibs_with_distinct_ids()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("ComServerWithTypeLibs")
                .WithSource()
                .WithProjectChanges(proj =>
                    proj.Root.Add(
                        new XElement("ItemGroup",
                            new XElement("ComHostTypeLibrary", new XAttribute("Include", "dummy1.tlb"), new XAttribute("Id", 1)),
                            new XElement("ComHostTypeLibrary", new XAttribute("Include", "dummy2.tlb"), new XAttribute("Id", 2)))));

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void It_fails_when_typelib_does_not_exist()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("ComServerWithTypeLibs")
                .WithSource()
                .WithProjectChanges(proj => proj.Root.Add(new XElement("ItemGroup", new XElement("ComHostTypeLibrary", new XAttribute("Include", "doesnotexist.tlb")))));

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1172: ");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void It_fails_when_typelib_is_invalid()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("ComServerWithTypeLibs")
                .WithSource()
                .WithProjectChanges(proj => proj.Root.Add(new XElement("ItemGroup", new XElement("ComHostTypeLibrary", new XAttribute("Include", "invalid.tlb")))));

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1173: ");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void It_copies_nuget_package_dependencies()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("ComServerWithDependencies")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp3.1");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "ComServerWithDependencies.dll",
                "ComServerWithDependencies.pdb",
                "ComServerWithDependencies.deps.json",
                "ComServerWithDependencies.comhost.dll",
                "ComServerWithDependencies.runtimeconfig.json",
                "ComServerWithDependencies.runtimeconfig.dev.json",
                "Newtonsoft.Json.dll"
            });
        }
    }
}
