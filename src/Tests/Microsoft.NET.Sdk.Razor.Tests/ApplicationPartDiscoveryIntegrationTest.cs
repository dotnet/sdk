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

namespace Microsoft.NET.Razor.Sdk.Tests
{
    public class ApplicationPartDiscoveryIntegrationTest : SdkTest
    {
        public ApplicationPartDiscoveryIntegrationTest(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void Build_ProjectWithDependencyThatReferencesMvc_AddsAttribute_WhenBuildingUsingDotnetMsbuild()
            => Build_ProjectWithDependencyThatReferencesMvc_AddsAttribute();

        [FullMSBuildOnlyFactAttribute]
        public void Build_ProjectWithDependencyThatReferencesMvc_AddsAttribute_WhenBuildingUsingDesktopMsbuild()
            => Build_ProjectWithDependencyThatReferencesMvc_AddsAttribute();

        private void Build_ProjectWithDependencyThatReferencesMvc_AddsAttribute()
        {
            var testAsset = "AppWithP2PReference";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();
            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(intermediateOutputPath, "AppWithP2PReference.MvcApplicationPartsAssemblyInfo.cs")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "AppWithP2PReference.MvcApplicationPartsAssemblyInfo.cs")).Should().Contain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute(\"ClassLibrary\")]");
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).AssemblyShould().HaveAttribute("Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute");
        }

        [Fact]
        public void Build_ProjectWithDependencyThatReferencesMvc_DoesNotGenerateAttributeIfFlagIsReset()
        {
            var testAsset = "AppWithP2PReference";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute("/p:GenerateMvcApplicationPartsAssemblyAttributes=false").Should().Pass();

            string intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();

            File.Exists(Path.Combine(intermediateOutputPath, "AppWithP2PReference.MvcApplicationPartsAssemblyInfo.cs")).Should().BeFalse();
        }

        [Fact]
        public void Build_ProjectWithoutMvcReferencingDependencies_DoesNotGenerateAttribute()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            string intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().FullName, "Debug", "net5.0");

            File.Exists(Path.Combine(intermediateOutputPath, "SimpleMvc.MvcApplicationPartsAssemblyInfo.cs")).Should().BeFalse();;

            // We should produced a cache file for build incrementalism
            File.Exists(Path.Combine(intermediateOutputPath, "SimpleMvc.MvcApplicationPartsAssemblyInfo.cache")).Should().BeTrue();
        }

        [Fact]
        public void BuildIncrementalism_WhenApplicationPartAttributeIsGenerated()
        {
            var testAsset = "AppWithP2PReference";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            string intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().FullName, "Debug", "net5.0");

            var generatedAttributeFile = Path.Combine(intermediateOutputPath, "AppWithP2PReference.MvcApplicationPartsAssemblyInfo.cs");
            var cacheFile = Path.Combine(intermediateOutputPath, "AppWithP2PReference.MvcApplicationPartsAssemblyInfo.cache");
            var outputFile = Path.Combine(intermediateOutputPath, "AppWithP2PReference.dll");
            File.Exists(generatedAttributeFile).Should().BeTrue();
            new FileInfo(generatedAttributeFile).Should().Contain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute(\"ClassLibrary\")]");

            var generatedFilethumbPrint = FileThumbPrint.Create(generatedAttributeFile);
            var cacheFileThumbPrint = FileThumbPrint.Create(cacheFile);
            var outputFileThumbPrint = FileThumbPrint.Create(outputFile);

            AssertIncrementalBuild();
            AssertIncrementalBuild();

            void AssertIncrementalBuild()
            {
                var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
                build.Execute().Should().Pass();

                var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

                File.Exists(generatedAttributeFile).Should().BeTrue();
                Assert.Equal(generatedFilethumbPrint, FileThumbPrint.Create(generatedAttributeFile));
                Assert.Equal(cacheFileThumbPrint, FileThumbPrint.Create(cacheFile));
                Assert.Equal(outputFileThumbPrint, FileThumbPrint.Create(outputFile));
                new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).AssemblyShould().HaveAttribute("Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute");
            }
        }

        // Regression test for https://github.com/dotnet/aspnetcore/issues/11315
        [Fact]
        public void BuildIncrementalism_CausingRecompilation_WhenApplicationPartAttributeIsGenerated()
        {
            var testAsset = "AppWithP2PReference";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            string intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();
            string outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            var generatedAttributeFile = Path.Combine(intermediateOutputPath, "AppWithP2PReference.MvcApplicationPartsAssemblyInfo.cs");
            File.Exists(generatedAttributeFile).Should().BeTrue();
            new FileInfo(generatedAttributeFile).Should().Contain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute(\"ClassLibrary\")]");

            var thumbPrint = FileThumbPrint.Create(generatedAttributeFile);

            // Touch a file in the main app which should call recompilation, but not the Mvc discovery tasks to re-run.
            File.AppendAllText(Path.Combine(build.ProjectRootPath, "Program.cs"), " ");
            
            build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            File.Exists(generatedAttributeFile).Should().BeTrue();
            Assert.Equal(thumbPrint, FileThumbPrint.Create(generatedAttributeFile));
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).AssemblyShould().HaveAttribute("Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute");
        }

        [Fact]
        public void BuildIncrementalism_WhenApplicationPartAttributeIsNotGenerated()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute()
                .Should()
                .Pass();

            string intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().FullName, "Debug", "net5.0");

            var generatedAttributeFile = Path.Combine(intermediateOutputPath, "SimpleMvc.MvcApplicationPartsAssemblyInfo.cs");
            var cacheFile = Path.Combine(intermediateOutputPath, "SimpleMvc.MvcApplicationPartsAssemblyInfo.cache");
            var outputFile = Path.Combine(intermediateOutputPath, "SimpleMvc.dll");
            File.Exists(generatedAttributeFile).Should().BeFalse();
            File.Exists(cacheFile).Should().BeTrue();

            var cacheFilethumbPrint = FileThumbPrint.Create(cacheFile);
            var outputFilethumbPrint = FileThumbPrint.Create(outputFile);

            // Couple rounds of incremental builds.
            AssertIncrementalBuild();
            AssertIncrementalBuild();
            AssertIncrementalBuild();

            void AssertIncrementalBuild()
            {
                build = new BuildCommand(projectDirectory);
                build.Execute()
                    .Should()
                    .Pass();

                File.Exists(generatedAttributeFile).Should().BeFalse();
                File.Exists(cacheFile).Should().BeTrue();

                Assert.Equal(cacheFilethumbPrint, FileThumbPrint.Create(cacheFile));
                Assert.Equal(outputFilethumbPrint, FileThumbPrint.Create(outputFile));
            }
        }

        [Fact]
        public void Build_ProjectWithMissingAssemblyReference_PrintsWarning()
        {
            var testAsset = "AppWithP2PReference";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute("/p:BuildProjectReferences=false")
                .Should()
                .Fail()
                .And.HaveStdOutContaining("CS0006")
                .And.HaveStdOutContaining("RAZORSDK1007");
        }
    }
}
