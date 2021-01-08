// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class MvcBuildIntegrationTest21 : MvcBuildIntegrationTestLegacy
    {
        public MvcBuildIntegrationTest21(ITestOutputHelper log) : base(log) { }

        public override string TestProjectName => "SimpleMvc21";
        public override string TargetFramework => "netcoreapp2.1";

        [Fact]
        public void Building_WorksWhenMultipleRazorConfigurationsArePresent()
        {
            var testAsset = TestProjectName;
            var project = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            project.WithProjectChanges(project =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "ItemGroup");
                var element = new XElement("RazorConfiguration", new XAttribute("Include", @"MVC-2.1"));
                element.Add(new XElement("Extensions", @"MVC-2.1;$(CustomRazorExtension"));
                element.Add(new XElement("CssScope", "b-overriden"));
                itemGroup.Add(element);
                project.Root.Add(itemGroup);
            });

            // Build
            var build = new BuildCommand(project);
            build.Execute().Should().Pass();

            var outputPath = build.GetOutputDirectory(TargetFramework, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "SimpleMvc21.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc21.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc21.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc21.Views.pdb")).Should().Exist();
        }

        [Fact]
        public void Build_DoesNotAddRelatedAssemblyPart_IfToolSetIsNotRazorSdk()
        {
            var testAsset = TestProjectName;
            var project = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(project);
            build.Execute("/p:RazorCompileToolSet=MvcPrecompilation").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", TargetFramework);
            var razorAssemblyInfo = Path.Combine(intermediateOutputPath, $"{TestProjectName}.RazorAssemblyInfo.cs");

            new FileInfo(razorAssemblyInfo).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, $"{TestProjectName}.RazorTargetAssemblyInfo.cs")).Should().NotExist();
        }

        [Fact]
        public void Publish_NoopsWithMvcRazorCompileOnPublish_False()
        {
            var testAsset = TestProjectName;
            var project = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            var publish = new PublishCommand(Log, project.TestRoot);
            publish.Execute("/p:MvcRazorCompileOnPublish=false").Should().Pass();

            var outputPath = publish.GetOutputDirectory(TargetFramework, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, OutputFileName)).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, $"{TestProjectName}.Views.pdb")).Should().NotExist();
        }

        [Fact] // This will use the old precompilation tool, RazorSDK shouldn't get involved.
        public void Build_WithMvcRazorCompileOnPublish_Noops()
        {
            var testAsset = TestProjectName;
            var project = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(project);
            build.Execute("/p:MvcRazorCompileOnPublish=true").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", TargetFramework);

            new FileInfo(Path.Combine(intermediateOutputPath, OutputFileName)).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, $"{TestProjectName}.pdb")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, $"{TestProjectName}.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, $"{TestProjectName}.Views.pdb")).Should().NotExist();
        }
    }
}
