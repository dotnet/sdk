using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAComServerLibrary : SdkTest
    {
        public GivenThatWeWantToBuildAComServerLibrary(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact()]
        public void It_copies_the_comhost_to_the_output_directory()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("ComServer")
                .WithSource()
                .Restore(Log);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute("/bl")
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp3.0");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "ComServer.dll",
                "ComServer.pdb",
                "ComServer.deps.json",
                "ComServer.comhost.dll"
            });
        }

        [Theory(Skip = "Waiting on comhost being merged into core-setup.")]
        [InlineData("win-x64")]
        [InlineData("win-x86")]
        public void It_copies_the_comhost_and_clsidmap_to_the_output_directory_when_not_embedding(string rid)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("ComServer")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement("_EmbedClsidMap", false));
                    propertyGroup.Add(new XElement("RuntimeIdentifier", rid));
                })
                .Restore(Log);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp3.0", runtimeIdentifier: rid);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "ComServer.dll",
                "ComServer.pdb",
                "ComServer.deps.json",
                "ComServer.comhost.dll",
                "ComServer.clsidmap"
            });
        }

        [Theory]
        [InlineData("linux-x64")]
        public void It_fails_for_platforms_without_comhost(string rid)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("ComServer")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement("RuntimeIdentifier", rid));
                })
                .Restore(Log);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute("/bl")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1088: ");
        }
    }
}
