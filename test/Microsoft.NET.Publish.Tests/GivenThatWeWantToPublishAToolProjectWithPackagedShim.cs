// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Publish.Tests
{
    [TestClass]
    public class GivenThatWeWantToPublishAToolProjectWithPackagedShim : SdkTest
    {
        private const string _customToolCommandName = "customToolCommandName";

        private TestAsset SetupTestAsset([CallerMemberName] string callingMethod = "")
        {
            TestAsset helloWorldAsset = TestAssetsManager
                .CopyTestAsset("PortableTool", callingMethod)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    XNamespace ns = project.Root.Name.Namespace;
                    XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "PackAsToolShimRuntimeIdentifiers", $"win-x64;{ToolsetInfo.LatestMacRuntimeIdentifier}-x64"));
                    propertyGroup.Add(new XElement(ns + "ToolCommandName", _customToolCommandName));
                });

            return helloWorldAsset;
        }

        [TestMethod]
        [DataRow("optimized", true)]
        [DataRow("full", true)]
        [DataRow("no-build", true)]
        [DataRow("optimized", false)]
        public void NativeAot_publish_includes_explicitly_requested_shims(string mode, bool requestShims)
        {
            var testProject = new TestProject
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                Name = "NativeAotToolWithShim",
                IsExe = true
            };
            testProject.SourceFiles["Program.cs"] = """
                System.Console.WriteLine("Hello World");
                """;
            testProject.AdditionalProperties["PublishAot"] = "true";
            testProject.AdditionalProperties["PackAsTool"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = RuntimeInformation.RuntimeIdentifier;
            testProject.AdditionalProperties["ToolCommandName"] = _customToolCommandName;
            if (requestShims)
            {
                testProject.AdditionalProperties["PackAsToolShimRuntimeIdentifiers"] = "win-x64";
            }
            if (mode == "full")
            {
                testProject.AdditionalProperties["UseOptimizedPublish"] = "false";
            }
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: $"{mode}_{requestShims}")
                .WithProjectChanges(project => project.Root.Add(XElement.Parse("""
                    <Target Name="RejectShimGenerationWithNoBuild" BeforeTargets="GenerateShimsAssets"
                            Condition="'$(NoBuild)' == 'true'">
                      <Error Text="NoBuild must reuse previously generated shims" />
                    </Target>
                    """)));
            if (mode == "no-build")
            {
                new BuildCommand(testAsset).Execute().Should().Pass();
            }

            var publishCommand = new PublishCommand(testAsset);
            string[] arguments = mode == "no-build" ? ["/p:NoBuild=true"] : [];
            publishCommand.Execute(arguments).Should().Pass();
            // Repeat to cover reconstruction of the shim items when generation is up to date.
            publishCommand.Execute(arguments).Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                ToolsetInfo.CurrentTargetFramework, runtimeIdentifier: RuntimeInformation.RuntimeIdentifier).FullName;
            File.Exists(Path.Combine(publishDirectory, "shims", "win-x64", _customToolCommandName + ".exe"))
                .Should().Be(requestShims);
            File.Exists(Path.Combine(publishDirectory, $"{testProject.Name}{Constants.ExeSuffix}")).Should().BeTrue();
            if (mode == "optimized")
            {
                Directory.GetFiles(Path.GetDirectoryName(publishDirectory)).Should().BeEmpty();
            }
        }

        [TestMethod]
        public void It_contains_dependencies_shims()
        {
            var testAsset = SetupTestAsset();
            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute();

            publishCommand.GetOutputDirectory(targetFramework: ToolsetInfo.CurrentTargetFramework)
                .Sub("shims")
                .Sub("win-x64")
                .EnumerateFiles().Should().Contain(f => f.Name == _customToolCommandName + ".exe");
        }

        [TestMethod]
        public void It_contains_dependencies_shims_with_no_build()
        {
            var testAsset = SetupTestAsset();
            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute();

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute("/p:NoBuild=true");

            publishCommand.GetOutputDirectory(targetFramework: ToolsetInfo.CurrentTargetFramework)
                .Sub("shims")
                .Sub("win-x64")
                .EnumerateFiles().Should().Contain(f => f.Name == _customToolCommandName + ".exe");
        }
    }
}
