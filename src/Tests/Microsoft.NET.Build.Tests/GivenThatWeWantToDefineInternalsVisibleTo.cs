// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.Xml.Linq;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToDefineInternalsVisibleTo : SdkTest
    {
        public GivenThatWeWantToDefineInternalsVisibleTo(ITestOutputHelper log) : base(log)
        { }

        private static readonly string testAssemblyName = "TestAssembly";
        private static readonly string testKeyName = "TestKey";

        [Fact]
        public void It_can_be_defined_in_proj_file()
        {
            TestProject testProject = new TestProject()
            {
                Name = "TestProject",
                IsSdkProject = true,
                IsExe = true,
                TargetFrameworks = "netcoreapp3.0"
            };
            testProject.AdditionalItems.Add("InternalsVisibleTo", testAssemblyName);

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            buildCommand.Execute()
                .Should()
                .Pass();

            var AssemblyInfoPath = Path.Combine(testAsset.TestRoot, testProject.Name, "obj", "Debug", testProject.TargetFrameworks, testProject.Name + ".AssemblyInfo.cs");
            File.Exists(AssemblyInfoPath).Should().BeTrue();
            var AssemblyInfoContent = File.ReadAllText(AssemblyInfoPath);
            // Test assembly should be in the auto generated assembly info file
            AssemblyInfoContent.Should().Contain(testAssemblyName);
            AssemblyInfoContent.Should().NotContain("PublicKey=");
        }

        [Fact] 
        public void It_can_be_defined_in_proj_file_with_key()
        {
            TestProject testProject = new TestProject()
            {
                Name = "TestProject",
                IsSdkProject = true,
                IsExe = true,
                TargetFrameworks = "netcoreapp3.0"
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            testAsset = testAsset
                .WithProjectChanges(project => AddInternalsVisibleToWithKey(project, string.Empty));

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            buildCommand.Execute()
                .Should()
                .Pass();

            var AssemblyInfoPath = Path.Combine(testAsset.TestRoot, testProject.Name, "obj", "Debug", testProject.TargetFrameworks, testProject.Name + ".AssemblyInfo.cs");
            File.Exists(AssemblyInfoPath).Should().BeTrue();
            var AssemblyInfoContent = File.ReadAllText(AssemblyInfoPath);
            // Test assembly should be in the auto generated assembly info file
            AssemblyInfoContent.Should().Contain(testAssemblyName);
            AssemblyInfoContent.Should().Contain("PublicKey=" + testKeyName);
        }

        [Fact]
        public void It_can_be_defined_in_proj_file_with_multiple_assemblies()
        {
            var assembliesCount = 5;

            TestProject testProject = new TestProject()
            {
                Name = "TestProject",
                IsSdkProject = true,
                IsExe = true,
                TargetFrameworks = "netcoreapp3.0"
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            for (int i = 0; i < assembliesCount; i++)
            {
                testAsset = testAsset
                    .WithProjectChanges(project => AddInternalsVisibleToWithKey(project, i.ToString()));
            }

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            buildCommand.Execute()
                .Should()
                .Pass();

            var AssemblyInfoPath = Path.Combine(testAsset.TestRoot, testProject.Name, "obj", "Debug", testProject.TargetFrameworks, testProject.Name + ".AssemblyInfo.cs");
            File.Exists(AssemblyInfoPath).Should().BeTrue();
            var AssemblyInfoContent = File.ReadAllText(AssemblyInfoPath);
            // Test assembly should be in the auto generated assembly info file
            for (int i = 0; i < assembliesCount; i++)
            {
                AssemblyInfoContent.Should().Contain(testAssemblyName + i);
                AssemblyInfoContent.Should().Contain("PublicKey=" + testKeyName + i);
            }
        }

        private void AddInternalsVisibleToWithKey(XDocument package, string identifier)
        {
            var ns = package.Root.Name.Namespace;
            XElement itemGroup = new XElement(ns + "ItemGroup");
            itemGroup.Add(new XElement(ns + "InternalsVisibleTo", new XAttribute("Include", testAssemblyName + identifier),
                new XAttribute("Key", testKeyName + identifier)));
            package.Root.Add(itemGroup);
        }
    }
}
