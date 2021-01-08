// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class BuildIntegrationTest : SdkTest
    {
        public BuildIntegrationTest(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void Build_SimpleMvc_UsingDotnetMSBuildAndWithoutBuildServer_CanBuildSuccessfully()
            => Build_SimpleMvc_WithoutBuildServer_CanBuildSuccessfully();

        [FullMSBuildOnlyFactAttribute]
        public void Build_SimpleMvc_UsingDesktopMSBuildAndWithoutBuildServer_CanBuildSuccessfully()
            => Build_SimpleMvc_WithoutBuildServer_CanBuildSuccessfully();

        // This test is identical to the ones in BuildServerIntegrationTest except this one explicitly disables the Razor build server.
        private void Build_SimpleMvc_WithoutBuildServer_CanBuildSuccessfully()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            build.Execute("/p:UseRazorBuildServer=false")
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"SimpleMvc -> {Path.Combine(projectDirectory.Path, outputPath, "SimpleMvc.Views.dll")}");

            new FileInfo(Path.Combine(outputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.pdb")).Should().Exist();
        }

        [Fact]
        public void Build_SimpleMvc_NoopsWithNoFiles()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            Directory.Delete(Path.Combine(projectDirectory.Path, "Views"), recursive: true);

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();
            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            new FileInfo(Path.Combine(outputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.pdb")).Should().NotExist();

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.pdb")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.RazorTargetAssemblyInfo.cs")).Should().NotExist();
        }

        [Fact]
        public void Build_SimpleMvc_NoopsWithRazorCompileOnBuild_False()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:RazorCompileOnBuild=false").Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.pdb")).Should().NotExist();
        }

        [Fact]
        public void Build_ErrorInGeneratedCode_ReportsMSBuildError()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var filePath = Path.Combine(projectDirectory.Path, "Views", "Home", "Index.cshtml");
            File.WriteAllText(filePath, "@{ var foo = \"\".Substring(\"bleh\"); }");

            var location = filePath + "(1,27)";
            var build = new BuildCommand(projectDirectory);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Absolute paths on OSX don't work well.
                build.Execute().Should().Fail().And.HaveStdOutContaining("CS1503");
            } else {
                build.Execute().Should().Fail().And.HaveStdOutContaining("CS1503").And.HaveStdOutContaining(location);
            }

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            // Compilation failed without creating the views assembly
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().NotExist();
        }

        [Fact]
        public void Build_Works_WhenFilesAtDifferentPathsHaveSameNamespaceHierarchy()
        {
            var testAsset = "SimplePages";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory, "SimplePages");
            build.Execute().Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            new FileInfo(Path.Combine(intermediateOutputPath, "SimplePages.dll")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimplePages.Views.dll")).Should().Exist();
        }

        [Fact]
        public void Build_RazorOutputPath_SetToNonDefault()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var customOutputPath = Path.Combine(projectDirectory.Path, "bin", "Debug", "net5.0", "Razor");

            var build = new BuildCommand(projectDirectory);
            build.Execute($"/p:RazorOutputPath={customOutputPath}").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().Exist();

            new FileInfo(Path.Combine(customOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();
            new FileInfo(Path.Combine(customOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
        }

        [Fact]
        public void Build_MvcRazorOutputPath_SetToNonDefault()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var customOutputPath = Path.Combine(projectDirectory.Path, "bin", "Debug", "net5.0", "Razor");

            var build = new BuildCommand(projectDirectory);
            build.Execute($"/p:MvcRazorOutputPath={customOutputPath}").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().Exist();

            new FileInfo(Path.Combine(customOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();
            new FileInfo(Path.Combine(customOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
        }

        [Fact]
        public void Build_SkipsCopyingBinariesToOutputDirectory_IfCopyBuildOutputToOutputDirectory_IsUnset()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:CopyBuildOutputToOutputDirectory=false").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");
            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().Exist();

            new FileInfo(Path.Combine(outputPath, "SimpleMvc.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.dll")).Should().NotExist();
        }

        [Fact]
        public void Build_SkipsCopyingBinariesToOutputDirectory_IfCopyOutputSymbolsToOutputDirectory_IsUnset()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:CopyOutputSymbolsToOutputDirectory=false").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");
            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();

            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.pdb")).Should().NotExist();
        }

        [Fact]
        public void Build_Works_WhenSymbolsAreNotGenerated()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:DebugType=none").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");
            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.pdb")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.pdb")).Should().NotExist();

            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.pdb")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.pdb")).Should().NotExist();
        }

        [Fact]
        public void Build_WithP2P_CopiesRazorAssembly()
        {
            var testAsset = "AppWithP2PReference";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.Views.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.pdb")).Should().Exist();
        }

        [Fact]
        public void Build_SetsUpEmbeddedResourcesWithLogicalName()
        {
            // Arrange
            var testAsset = "SimplePages";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .WithProjectChanges(project => {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "ItemGroup");
                    itemGroup.Add(new XElement("Content", new XAttribute("Include", "..\\LinkedDir\\LinkedFile.cshtml"), new XAttribute("Link", "LinkedFileOut\\LinkedFile.cshtml")));
                    project.Root.Add(itemGroup);
                });

            var build = new BuildCommand(projectDirectory, "SimplePages");
            build.Execute("/t:_IntrospectRazorEmbeddedResources", "/p:EmbedRazorGenerateSources=true").Should().Pass()
                .And.HaveStdOutContaining($@"CompileResource: {Path.Combine("Pages", "Index.cshtml")} /Pages/Index.cshtml")
                .And.HaveStdOutContaining($@"CompileResource: {Path.Combine("Areas", "Products", "Pages", "_ViewStart.cshtml")} /Areas/Products/Pages/_ViewStart.cshtml")
                .And.HaveStdOutContaining($@"CompileResource: {Path.Combine("..", "LinkedDir", "LinkedFile.cshtml")} /LinkedFileOut/LinkedFile.cshtml");
        }

        [Fact]
        public void Build_WithViews_ProducesDepsFileWithCompilationContext_ButNoReferences()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var customDefine = "RazorSdkTest";
            var build = new BuildCommand(projectDirectory);
            build.Execute($"/p:DefineConstants={customDefine}").Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "SimpleMvc.deps.json")).Should().Exist();
            var depsFilePath = Path.Combine(outputPath, "SimpleMvc.deps.json");
            var dependencyContext = ReadDependencyContext(depsFilePath);

            // Pick a couple of libraries and ensure they have some compile references
            var packageReference = dependencyContext.CompileLibraries.First(l => l.Name == "System.Diagnostics.DiagnosticSource");
            packageReference.Assemblies.Should().NotBeEmpty();

            var projectReference = dependencyContext.CompileLibraries.First(l => l.Name == "SimpleMvc");
            projectReference.Assemblies.Should().NotBeEmpty();

            dependencyContext.CompilationOptions.Defines.Should().Contain(customDefine);

            // Verify no refs folder is produced
            new DirectoryInfo(Path.Combine(outputPath, "publish", "refs")).Should().NotExist();
        }

        [Fact]
        public void Build_WithPreserveCompilationReferencesEnabled_ProducesRefsDirectory()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .WithProjectChanges(project => {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "PropertyGroup");
                    itemGroup.Add(new XElement("PreserveCompilationReferences", "true"));
                    project.Root.Add(itemGroup);
                });

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "refs", "mscorlib.dll")).Should().Exist();
        }

        [Fact]
        public void Build_CodeGensAssemblyInfoUsingValuesFromProject()
        {
            var testAsset = "ClassLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();
            var razorAssemblyInfo = Path.Combine(intermediateOutputPath, "ClassLibrary.RazorTargetAssemblyInfo.cs");

            new FileInfo(razorAssemblyInfo).Should().Exist();
            new FileInfo(razorAssemblyInfo).Should().Contain("[assembly: System.Reflection.AssemblyCopyrightAttribute(\"© Microsoft\")]");
            new FileInfo(razorAssemblyInfo).Should().Contain("[assembly: System.Reflection.AssemblyProductAttribute(\"Razor Test\")]");
            new FileInfo(razorAssemblyInfo).Should().Contain("[assembly: System.Reflection.AssemblyCompanyAttribute(\"Microsoft\")]");
            new FileInfo(razorAssemblyInfo).Should().Contain("[assembly: System.Reflection.AssemblyTitleAttribute(\"ClassLibrary.Views\")]");
            new FileInfo(razorAssemblyInfo).Should().Contain("[assembly: System.Reflection.AssemblyVersionAttribute(\"1.0.0.0\")]");
            new FileInfo(razorAssemblyInfo).Should().Contain("[assembly: System.Reflection.AssemblyFileVersionAttribute(\"1.0.0.0\")]");
            new FileInfo(razorAssemblyInfo).Should().Contain("[assembly: System.Reflection.AssemblyInformationalVersionAttribute(\"1.0.0\")]");
            new FileInfo(razorAssemblyInfo).Should().Contain("[assembly: System.Reflection.AssemblyDescriptionAttribute(\"ClassLibrary Description\")]");
        }

        [Fact]
        public void Build_UsesRazorSpecificAssemblyProperties()
        {
            var testAsset = "ClassLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory);
            build
                .Execute("/p:RazorAssemblyTitle=MyRazorViews", "/p:RazorAssemblyFileVersion=2.0.0.100", "/p:RazorAssemblyInformationalVersion=2.0.0-preview",
                "/p:RazorAssemblyVersion=2.0.0", "/p:RazorAssemblyDescription=MyRazorDescription")
                .Should()
                .Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();
            var razorTargetAssemblyInfo = Path.Combine(intermediateOutputPath, "ClassLibrary.RazorTargetAssemblyInfo.cs");

            new FileInfo(razorTargetAssemblyInfo).Should().Exist();

            new FileInfo(razorTargetAssemblyInfo).Should().Contain("[assembly: System.Reflection.AssemblyDescriptionAttribute(\"MyRazorDescription\")]");
            new FileInfo(razorTargetAssemblyInfo).Should().Contain("[assembly: System.Reflection.AssemblyTitleAttribute(\"MyRazorViews\")]");
            new FileInfo(razorTargetAssemblyInfo).Should().Contain("[assembly: System.Reflection.AssemblyVersionAttribute(\"2.0.0\")]");
            new FileInfo(razorTargetAssemblyInfo).Should().Contain("[assembly: System.Reflection.AssemblyFileVersionAttribute(\"2.0.0.100\")]");
            new FileInfo(razorTargetAssemblyInfo).Should().Contain("[assembly: System.Reflection.AssemblyInformationalVersionAttribute(\"2.0.0-preview\")]");
        }

        [Fact]
        public void Build_DoesNotGenerateAssemblyInfo_IfGenerateRazorTargetAssemblyInfo_IsSetToFalse()
        {
            var testAsset = "ClassLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:GenerateRazorTargetAssemblyInfo=false").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(intermediateOutputPath, "ClassLibrary.AssemblyInfo.cs")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "ClassLibrary.RazorTargetAssemblyInfo.cs")).Should().NotExist();
        }

        [Fact]
        public void Build_AddsApplicationPartAttributes()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();

            var razorAssemblyInfoPath = Path.Combine(intermediateOutputPath, "SimpleMvc.RazorAssemblyInfo.cs");
            var razorTargetAssemblyInfo = Path.Combine(intermediateOutputPath, "SimpleMvc.RazorTargetAssemblyInfo.cs");

            new FileInfo(razorAssemblyInfoPath).Should().Exist();
            new FileInfo(razorAssemblyInfoPath).Should().Contain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.RelatedAssemblyAttribute(\"SimpleMvc.Views\")]");

            new FileInfo(razorTargetAssemblyInfo).Should().Exist();
            new FileInfo(razorTargetAssemblyInfo).Should().Contain("[assembly: System.Reflection.AssemblyTitleAttribute(\"SimpleMvc.Views\")]");
            new FileInfo(razorTargetAssemblyInfo).Should().Contain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ProvideApplicationPartFactoryAttribute(\"Microsoft.AspNetCore.Mvc.ApplicationParts.CompiledRazorAssemblyApplicationPartFac\"");
        }

        [Fact]
        public void Build_AddsApplicationPartAttributes_WhenEnableDefaultRazorTargetAssemblyInfoAttributes_IsFalse()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:EnableDefaultRazorTargetAssemblyInfoAttributes=false").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();
            var razorTargetAssemblyInfo = Path.Combine(intermediateOutputPath, "SimpleMvc.RazorTargetAssemblyInfo.cs");

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.AssemblyInfo.cs")).Should().Exist();

            new FileInfo(razorTargetAssemblyInfo).Should().Exist();
            new FileInfo(razorTargetAssemblyInfo).Should().NotContain("[assembly: System.Reflection.AssemblyTitleAttribute");
            new FileInfo(razorTargetAssemblyInfo).Should().Contain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ProvideApplicationPartFactoryAttribute(\"Microsoft.AspNetCore.Mvc.ApplicationParts.CompiledRazorAssemblyApplicationPartFac\"");
        }

        [Fact]
        public void Build_UsesSpecifiedApplicationPartFactoryTypeName()
        {
            var testAsset = "ClassLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:ProvideApplicationPartFactoryAttributeTypeName=CustomFactory").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();
            var razorTargetAssemblyInfo = Path.Combine(intermediateOutputPath, "ClassLibrary.RazorTargetAssemblyInfo.cs");

            new FileInfo(razorTargetAssemblyInfo).Should().Exist();
            new FileInfo(razorTargetAssemblyInfo).Should().Contain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ProvideApplicationPartFactoryAttribute(\"CustomFactory\"");
        }

        [Fact]
        public void Build_DoesNotGenerateProvideApplicationPartFactoryAttribute_IfGenerateProvideApplicationPartFactoryAttributeIsUnset()
        {
            var testAsset = "ClassLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:GenerateProvideApplicationPartFactoryAttribute=false").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();
            var razorTargetAssemblyInfo = Path.Combine(intermediateOutputPath, "ClassLibrary.RazorTargetAssemblyInfo.cs");
        
            new FileInfo(razorTargetAssemblyInfo).Should().Exist();
            new FileInfo(razorTargetAssemblyInfo).Should().NotContain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ProvideApplicationPartFactoryAttribute");
        }

        [Fact]
        public void Build_DoesNotAddRelatedAssemblyPart_IfViewCompilationIsDisabled()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:RazorCompileOnBuild=false", "/p:RazorCompileOnPublish=false").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();
            var razorAssemblyInfo = Path.Combine(intermediateOutputPath, "SimpleMvc.RazorAssemblyInfo.cs");

            new FileInfo(razorAssemblyInfo).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.RazorTargetAssemblyInfo.cs")).Should().NotExist();
        }

        [Fact]
        public void Build_AddsRelatedAssemblyPart_IfCompileOnPublishIsAllowed()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:RazorCompileOnBuild=false").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();
            var razorAssemblyInfo = Path.Combine(intermediateOutputPath, "SimpleMvc.RazorAssemblyInfo.cs");

            new FileInfo(razorAssemblyInfo).Should().Exist();
            new FileInfo(razorAssemblyInfo).Should().Contain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.RelatedAssemblyAttribute(\"SimpleMvc.Views\")]");

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.RazorTargetAssemblyInfo.cs")).Should().NotExist();
        }

        [Fact]
        public void Build_AddsRelatedAssemblyPart_IfGenerateAssemblyInfoIsFalse()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:GenerateAssemblyInfo=false").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();
            var razorAssemblyInfo = Path.Combine(intermediateOutputPath, "SimpleMvc.RazorAssemblyInfo.cs");

            new FileInfo(razorAssemblyInfo).Should().Exist();
            new FileInfo(razorAssemblyInfo).Should().Contain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.RelatedAssemblyAttribute(\"SimpleMvc.Views\")]");
        }

        [Fact]
        public void Build_WithGenerateRazorHostingAssemblyInfoFalse_DoesNotGenerateHostingAttributes()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:GenerateRazorHostingAssemblyInfo=false").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();
            var razorAssemblyInfo = Path.Combine(intermediateOutputPath, "SimpleMvc.RazorAssemblyInfo.cs");

            new FileInfo(razorAssemblyInfo).Should().Exist();
            
            new FileInfo(razorAssemblyInfo).Should().NotContain("Microsoft.AspNetCore.Razor.Hosting.RazorLanguageVersionAttribute");
            new FileInfo(razorAssemblyInfo).Should().NotContain("Microsoft.AspNetCore.Razor.Hosting.RazorConfigurationNameAttribute");
            new FileInfo(razorAssemblyInfo).Should().NotContain("Microsoft.AspNetCore.Razor.Hosting.RazorExtensionAssemblyNameAttribute");
            new FileInfo(razorAssemblyInfo).Should().Contain("[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.RelatedAssemblyAttribute(\"SimpleMvc.Views\")]");
        }

        [Fact]
        public void Build_DoesNotGenerateHostingAttributes_InClassLibraryProjects()
        {
            var testAsset = "ClassLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();
            var razorAssemblyInfo = Path.Combine(intermediateOutputPath, "ClassLibrary.AssemblyInfo.cs");


            new FileInfo(razorAssemblyInfo).Should().Exist();
            new FileInfo(razorAssemblyInfo).Should().NotContain("Microsoft.AspNetCore.Razor.Hosting.RazorLanguageVersionAttribute");
            new FileInfo(razorAssemblyInfo).Should().NotContain("Microsoft.AspNetCore.Razor.Hosting.RazorConfigurationNameAttribute");
            new FileInfo(razorAssemblyInfo).Should().NotContain("Microsoft.AspNetCore.Razor.Hosting.RazorExtensionAssemblyNameAttribute");
        }

        [Fact]
        public void Build_GeneratesHostingAttributes_WhenGenerateRazorHostingAssemblyInfoIsSet()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:GenerateRazorHostingAssemblyInfo=false").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();
            var razorAssemblyInfo = Path.Combine(intermediateOutputPath, "SimpleMvc.AssemblyInfo.cs");

            new FileInfo(razorAssemblyInfo).Should().Exist();

            new FileInfo(razorAssemblyInfo).Should().NotContain("Microsoft.AspNetCore.Razor.Hosting.RazorLanguageVersionAttribute");
            new FileInfo(razorAssemblyInfo).Should().NotContain("Microsoft.AspNetCore.Razor.Hosting.RazorConfigurationNameAttribute");
        }

        [Fact]
        public void Build_WithGenerateRazorAssemblyInfo_False_DoesNotGenerateAssemblyInfo()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:GenerateRazorAssemblyInfo=false").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();

            var razorAssemblyInfo = Path.Combine(intermediateOutputPath, "SimpleMvc.RazorAssemblyInfo.cs");

            new FileInfo(razorAssemblyInfo).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();  
        }

        [Fact]
        public void Build_WithGenerateRazorTargetAssemblyInfo_False_DoesNotGenerateAssemblyInfo()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:GenerateRazorTargetAssemblyInfo=false").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();

            var razorAssemblyInfo = Path.Combine(intermediateOutputPath, "SimpleMvc.RazorAssemblyInfo.cs");
            var razorTargetAssemblyInfo = Path.Combine(intermediateOutputPath, "SimpleMvc.RazorTargetAssemblyInfo.cs");

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();

            new FileInfo(razorAssemblyInfo).Should().Exist();
            new FileInfo(razorTargetAssemblyInfo).Should().NotExist();
        }

        [Fact]
        public void Build_WithP2P_WorksWhenBuildProjectReferencesIsDisabled()
        {
            var testAsset = "AppWithP2PReference";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .WithProjectChanges((path, project) =>
                {
                    if (path.Contains("AppWithP2PReference")) {
                        var ns = project.Root.Name.Namespace;
                        var itemGroup = new XElement(ns + "ItemGroup");
                        itemGroup.Add(new XElement("ProjectReference", new XAttribute("Include", "..\\AnotherClassLib\\AnotherClassLib.csproj")));
                        project.Root.Add(itemGroup);
                    }
                });;

            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AnotherClassLib.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AnotherClassLib.Views.dll")).Should().Exist();

            // Force a rebuild of ClassLibrary2 by changing a file
            var class2Path = Path.Combine(projectDirectory.Path, "AnotherClassLib", "Class2.cs");
            File.AppendAllText(class2Path, Environment.NewLine + "// Some changes");

            // dotnet msbuild /p:BuildProjectReferences=false
            build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute("/p:BuildProjectReferences=false").Should().Pass();
        }

        [Fact]
        public void Build_WithP2P_Referencing21Project_Works()
        {
            // Verifies building with different versions of Razor.Tasks works. Loosely modeled after the repro
            // scenario listed in https://github.com/Microsoft/msbuild/issues/3572
            var testAsset = "AppWithP2PReference";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .WithProjectChanges((path, project) =>
                {
                    if (path.Contains("AppWithP2PReference"))
                    {
                        var ns = project.Root.Name.Namespace;
                        var itemGroup = new XElement(ns + "ItemGroup");
                        itemGroup.Add(new XElement("ProjectReference", new XAttribute("Include", "..\\ClassLibraryMvc21\\ClassLibraryMvc21.csproj")));
                        project.Root.Add(itemGroup);
                    }
                });

            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibraryMvc21.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibraryMvc21.Views.dll")).Should().Exist();
        }

        [Fact]
        public void Build_WithStartupObjectSpecified_Works()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:StartupObject=SimpleMvc.Program").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();
        }

        [Fact]
        public void Build_WithDeterministicFlagSet_OutputsDeterministicViewsAssembly()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory);
            build.Execute($"/p:Deterministic=true").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            var filePath = Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll");
            var firstAssemblyBytes = File.ReadAllBytes(filePath);

            // Build 2
            build = new BuildCommand(projectDirectory);
            build.Execute($"/p:Deterministic=true").Should().Pass();

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().Exist();

            var secondAssemblyBytes = File.ReadAllBytes(filePath);
            Assert.Equal(firstAssemblyBytes, secondAssemblyBytes);
        }

        [Fact]
        public void Build_WithoutServer_ErrorDuringBuild_DisplaysErrorInMsBuildOutput()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:UseRazorBuildServer=false", "/p:RazorLangVersion=99.0")
                .Should()
                .Fail()
                .And.HaveStdOutContaining($"Invalid option 99.0 for Razor language version --version; must be Latest or a valid version in range 1.0 to 5.0."); 

            var intermediateOutputPath = build.GetIntermediateDirectory("net5.0", "Debug").ToString();

            // Compilation failed without creating the views assembly
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().NotExist();
        }

        [Fact]
        public void Build_CSharp8_NullableEnforcement_WarningsDuringBuild_NoBuildServer()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:LangVersion=8.0", "/p:Nullable=enable").Should().Pass().And.HaveStdOutContaining("CS8618");

            var indexFilePath = Path.Combine(build.GetIntermediateDirectory("net5.0", "Debug").ToString(), "Razor", "Views", "Home", "Index.cshtml.g.cs");

            new FileInfo(indexFilePath).Should().Contain("#nullable restore");
            new FileInfo(indexFilePath).Should().Contain("#nullable disable");
        }

        [Fact]
        public void Build_Mvc_WithoutAddRazorSupportForMvc()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:AddRazorSupportForMvc=false").Should().HaveStdOutContaining("RAZORSDK1004");
        }

        [Fact]
        public void Build_WithNoResolvedRazorConfiguration()
        {
            var testAsset = "ClassLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "PropertyGroup");
                    itemGroup.Add(new XElement("RazorDefaultConfiguration", "Custom12.3"));
                    project.Root.Add(itemGroup);
                });

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().HaveStdOutContaining("RAZORSDK1000");
        }

        private static DependencyContext ReadDependencyContext(string depsFilePath)
        {
            var reader = new DependencyContextJsonReader();
            using (var stream = File.OpenRead(depsFilePath))
            {
                return reader.Read(stream);
            }
        }
    }
}
