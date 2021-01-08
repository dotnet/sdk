// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class BuildWithComponentsIntegrationTest : SdkTest
    {
        public BuildWithComponentsIntegrationTest(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void Build_Components_WithDotNetCoreMSBuild_Works() => Build_ComponentsWorks();

        [FullMSBuildOnlyFact]
        public void Build_Components_WithDesktopMSBuild_Works() => Build_ComponentsWorks();

        [Fact]
        public void Build_DoesNotProduceMvcArtifacts_IfProjectDoesNotContainRazorGenerateItems()
        {
            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            string outputPath = build.GetOutputDirectory("net5.0").ToString();
            string intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().FullName, "Debug", "net5.0");

            new FileInfo(Path.Combine(outputPath, "ComponentApp.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ComponentApp.pdb")).Should().Exist();

            // Verify component compilation succeeded
            new FileInfo(Path.Combine(outputPath, "ComponentApp.dll")).AssemblyShould().ContainType("ComponentApp.Components.Pages.Counter");

            // Verify MVC artifacts do not appear in the output.
            new FileInfo(Path.Combine(outputPath, "ComponentApp.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "ComponentApp.Views.pdb")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.RazorAssemblyInfo.cs")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.RazorTargetAssemblyInfo.cs")).Should().NotExist();
        }

        [Fact]
        public void Build_Successful_WhenThereAreWarnings()
        {
            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var indexPage = Path.Combine(projectDirectory.Path, "Components", "Pages", "Index.razor");
            File.WriteAllText(indexPage, "<UnrecognizedComponent />");

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass().And.HaveStdOutContaining("RZ10012");

            string outputPath = build.GetOutputDirectory("net5.0").ToString();

            new FileInfo(Path.Combine(outputPath, "ComponentApp.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ComponentApp.pdb")).Should().Exist();

            // Verify component compilation succeeded
            new FileInfo(Path.Combine(outputPath, "ComponentApp.dll")).AssemblyShould().ContainType("ComponentApp.Components.Pages.Counter");
        }

        [Fact]
        public void Build_WithoutRazorLangVersion_ProducesWarning()
        {
            var testAsset = "ComponentLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:RazorLangVersion=").Should().Pass().And.HaveStdOutContaining("RAZORSDK1005");
        }

        [Fact]
        public void Building_NetstandardComponentLibrary()
        {
            var testAsset = "ComponentLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            // Build
            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            string outputPath = build.GetOutputDirectory("netstandard2.0").ToString();

            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.pdb")).Should().Exist();

            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.Views.pdb")).Should().NotExist();
        }

        [Fact]
        public void Build_DoesNotProduceRefsDirectory()
        {
            var testAsset = "ComponentLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            // Build
            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            string outputPath = build.GetOutputDirectory("netstandard2.0").ToString();

            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.dll")).Should().Exist();
            new DirectoryInfo(Path.Combine(outputPath, "refs")).Should().NotExist();
        }

        [Fact]
        public void Publish_DoesNotProduceRefsDirectory()
        {
            var testAsset = "ComponentLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new PublishCommand(Log, projectDirectory.Path);
            build.Execute().Should().Pass();

            string outputPath = build.GetOutputDirectory("netstandard2.0").ToString();

            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.dll")).Should().Exist();
            new DirectoryInfo(Path.Combine(outputPath, "refs")).Should().NotExist();
        }

        private void Build_ComponentsWorks()
        {
            var testAsset = "MvcWithComponents";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            string outputPath = build.GetOutputDirectory("net5.0").ToString();

            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.Views.pdb")).Should().Exist();

            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).AssemblyShould().ContainType("MvcWithComponents.TestComponent");
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).AssemblyShould().ContainType("MvcWithComponents.Views.Shared.NavMenu");

            // This is a component file with a .cshtml extension. It should appear in the main assembly, but not in the views dll.
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).AssemblyShould().ContainType("MvcWithComponents.Components.Counter");
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.Views.dll")).AssemblyShould().NotContainType("MvcWithComponents.Components.Counter");
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.Views.dll")).AssemblyShould().NotContainType("AspNetCore.Components_Counter");

            // Verify a regular View appears in the views dll, but not in the main assembly.
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).AssemblyShould().NotContainType("AspNetCore.Views.Home.Index");
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).AssemblyShould().NotContainType("AspNetCore.Views_Home_Index");
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.Views.dll")).AssemblyShould().ContainType("AspNetCore.Views_Home_Index");
        }
    }
}
