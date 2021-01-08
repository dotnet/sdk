// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
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
    public class PublishIntegrationTest : SdkTest
    {
        public PublishIntegrationTest(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void Publish_RazorCompileOnPublish_IsDefault()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute().Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();

            // Verify assets get published
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "js", "SimpleMvc.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "css", "site.css")).Should().Exist();

            // By default refs and .cshtml files will not be copied on publish
            new DirectoryInfo(Path.Combine(publishOutputPath, "refs")).Should().NotExist();
            new DirectoryInfo(Path.Combine(publishOutputPath, "Views")).Should().NotExist();
        }

        [Fact]
        public void Publish_PublishesAssemblyAndContent()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute().Should().Pass();

            var outputPath = Path.Combine(projectDirectory.Path, "bin", "Debug", "net5.0");
            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "appsettings.json")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "appsettings.Development.json")).Should().Exist();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "appsettings.json")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "appsettings.Development.json")).Should().Exist();

            // By default refs and .cshtml files will not be copied on publish
            new DirectoryInfo(Path.Combine(publishOutputPath, "refs")).Should().NotExist();
            new DirectoryInfo(Path.Combine(publishOutputPath, "Views")).Should().NotExist();
        }

        [Fact]
        public void Publish_PublishesAssembly()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute().Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();

            // By default refs and .cshtml files will not be copied on publish
             new DirectoryInfo(Path.Combine(publishOutputPath, "refs")).Should().NotExist();
            new DirectoryInfo(Path.Combine(publishOutputPath, "Views")).Should().NotExist();
        }

        [Fact]
        public void Publish_WithRazorCompileOnBuildFalse_PublishesAssembly()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:RazorCompileOnBuild=false").Should().Pass();

            var outputPath = Path.Combine(projectDirectory.Path, "bin", "Debug", "net5.0");
            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            // RazorCompileOnBuild is turned off, but RazorCompileOnPublish should still be enable
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "SimpleMvc.Views.pdb")).Should().NotExist();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();

            // By default refs and .cshtml files will not be copied on publish
            new DirectoryInfo(Path.Combine(publishOutputPath, "refs")).Should().NotExist();
            new DirectoryInfo(Path.Combine(publishOutputPath, "Views")).Should().NotExist();
        }

        [Fact] // This is an override to force the new toolset
        public void Publish_WithMvcRazorCompileOnPublish_AndRazorSDK_PublishesAssembly()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:MvcRazorCompileOnPublish=true", "/p:ResolvedRazorCompileToolset=RazorSDK").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();

            // By default refs and .cshtml files will not be copied on publish
            new DirectoryInfo(Path.Combine(publishOutputPath, "refs")).Should().NotExist();
            new DirectoryInfo(Path.Combine(publishOutputPath, "Views")).Should().NotExist();
        }

        [Fact]
        public void Publish_NoopsWithNoFiles()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            Directory.Delete(Path.Combine(projectDirectory.Path, "Views"), recursive: true);

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute().Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            // Everything we do should noop - including building the app.
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.pdb")).Should().NotExist();

            // By default refs will not be copied on publish
            new DirectoryInfo(Path.Combine(publishOutputPath, "refs")).Should().NotExist();
        }

        [Fact]
        public void Publish_NoopsWith_RazorCompileOnPublishFalse()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            Directory.Delete(Path.Combine(projectDirectory.Path, "Views"), recursive: true);

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:RazorCompileOnPublish=false").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            // Everything we do should noop - including building the app.
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.pdb")).Should().NotExist();
        }

        [Fact]
        public void Publish_SkipsCopyingBinariesToOutputDirectory_IfCopyBuildOutputToOutputDirectory_IsUnset()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:CopyBuildOutputToPublishDirectory=false").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();
            var intermediateOutputPath = Path.Combine(publish.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.dll")).Should().Exist();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().NotExist();
        }

        [Fact]
        public void Publish_SkipsCopyingBinariesToOutputDirectory_IfCopyOutputSymbolsToOutputDirectory_IsUnset()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:CopyOutputSymbolsToPublishDirectory=false").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();
            var intermediateOutputPath = Path.Combine(publish.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.pdb")).Should().NotExist();
        }

        [Fact]
        public void Publish_Works_WhenSymbolsAreNotGenerated()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:DebugType=none").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();
            var intermediateOutputPath = Path.Combine(publish.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.pdb")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc.Views.pdb")).Should().NotExist();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.pdb")).Should().NotExist();
        }

        [Fact]
        public void Publish_IncludeCshtmlAndRefAssemblies_CopiesFiles()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:CopyRazorGenerateFilesToPublishDirectory=true", "/p:CopyRefAssembliesToPublishDirectory=true").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();
            var intermediateOutputPath = Path.Combine(publish.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();

            // By default refs and .cshtml files will not be copied on publish
            new FileInfo(Path.Combine(publishOutputPath, "refs", "mscorlib.dll")).Should().Exist();
            new DirectoryInfo(Path.Combine(publishOutputPath, "Views")).Should().NotBeEmpty();
        }

        [Fact]
        public void Publish_WithCopySettingsInProjectFile_CopiesFiles()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "PropertyGroup");
                    itemGroup.Add(new XElement("CopyRefAssembliesToPublishDirectory", true));
                    itemGroup.Add(new XElement("CopyRazorGenerateFilesToPublishDirectory", true));
                    project.Root.Add(itemGroup);
                });

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute().Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();

            // By default refs and .cshtml files will not be copied on publish
            new FileInfo(Path.Combine(publishOutputPath, "refs", "mscorlib.dll")).Should().Exist();
            new DirectoryInfo(Path.Combine(publishOutputPath, "Views")).Should().NotBeEmpty();
        }

        [Fact]
        public void Publish_WithPreserveCompilationReferencesSetInProjectFile_CopiesRefs()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "PropertyGroup");
                    itemGroup.Add(new XElement("PreserveCompilationReferences", true));
                    project.Root.Add(itemGroup);
                });


            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute().Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();

            // By default refs and .cshtml files will not be copied on publish
            new FileInfo(Path.Combine(publishOutputPath, "refs", "mscorlib.dll")).Should().Exist();
        }

        [Fact] // Tests old MvcPrecompilation behavior that we support for compat.
        public void Publish_MvcRazorExcludeFilesFromPublish_False_CopiesFiles()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:MvcRazorExcludeViewFilesFromPublish=false", "/p:MvcRazorExcludeRefAssembliesFromPublish=false").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();

            // By default refs and .cshtml files will not be copied on publish
            new FileInfo(Path.Combine(publishOutputPath, "refs", "mscorlib.dll")).Should().Exist();
            new DirectoryInfo(Path.Combine(publishOutputPath, "Views")).Should().NotBeEmpty();
        }

        [Fact]
        public void Publish_WithP2P_AndRazorCompileOnBuild_CopiesRazorAssembly()
        {
            var testAsset = "AppWithP2PReference";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var publish = new PublishCommand(Log, Path.Combine(projectDirectory.TestRoot, "AppWithP2PReference"));
            publish.Execute().Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.Views.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.Views.pdb")).Should().Exist();
        }

        [Fact]
        public void Publish_WithP2P_AndRazorCompileOnPublish_CopiesRazorAssembly()
        {
            var testAsset = "AppWithP2PReference";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var publish = new PublishCommand(Log, Path.Combine(projectDirectory.TestRoot, "AppWithP2PReference"));
            publish.Execute().Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.Views.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.Views.pdb")).Should().Exist();

            // Verify fix for https://github.com/aspnet/Razor/issues/2295. No cshtml files should be published from the app
            // or the ClassLibrary.
            new DirectoryInfo(Path.Combine(publishOutputPath, "refs")).Should().NotExist();
            new DirectoryInfo(Path.Combine(publishOutputPath, "Views")).Should().NotExist();
        }


        [Fact]
        public void Publish_DoesNotPublishCustomRazorGenerateItems()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    propertyGroup.Add(new XElement("EnableDefaultRazorGenerateItems", false));

                    var itemGroup = new XElement(ns + "ItemGroup");
                    itemGroup.Add(new XElement("RazorGenerate", new XAttribute("Include", "Views\\_ViewImports.cshtml")));
                    itemGroup.Add(new XElement("RazorGenerate", new XAttribute("Include", "Views\\Home\\Index.cshtml")));

                    project.Root.Add(propertyGroup);
                    project.Root.Add(itemGroup);
                });;

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute().Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.pdb")).Should().Exist();

            // Verify assets get published
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "js", "SimpleMvc.js")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "css", "site.css")).Should().Exist();

            // By default refs and .cshtml files will not be copied on publish
            new DirectoryInfo(Path.Combine(publishOutputPath, "refs")).Should().NotExist();
            // Custom RazorGenerate item does not get published
            new FileInfo(Path.Combine(publishOutputPath, "Views", "Home", "Home.cshtml")).Should().NotExist();
            // cshtml Content item that's not part of RazorGenerate gets published.
            new FileInfo(Path.Combine(publishOutputPath, "Views", "Home", "About.cshtml")).Should().Exist();
        }

        [Fact]
        public void Publish_WithP2P_WorksWhenBuildProjectReferencesIsDisabled()
        {
            // Simulates publishing the same way VS does by setting BuildProjectReferences=false.
            // With this flag, P2P references aren't resolved during GetCopyToPublishDirectoryItems which would cause
            // any target that uses References as inputs to not be incremental. This test verifies no Razor Sdk work
            // is performed at this time.
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
                        itemGroup.Add(new XElement("ProjectReference", new XAttribute("Include", "..\\AnotherClassLib\\AnotherClassLib.csproj")));
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
            new FileInfo(Path.Combine(outputPath, "AnotherClassLib.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AnotherClassLib.Views.dll")).Should().Exist();

            // dotnet msbuild /t:Publish /p:BuildProjectReferences=false
            var publish = new PublishCommand(Log, $"{projectDirectory.TestRoot}/AppWithP2PReference");
            publish.Execute("/p:BuildProjectReferences=false").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "AppWithP2PReference.Views.pdb")).Should().Exist();

            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "ClassLibrary.Views.pdb")).Should().Exist();

            new FileInfo(Path.Combine(publishOutputPath, "AnotherClassLib.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "AnotherClassLib.pdb")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "AnotherClassLib.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "AnotherClassLib.Views.pdb")).Should().Exist();
        }

        [Fact]
        public void Publish_WithNoBuild_FailsWithoutBuild()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:NoBuild=true").Should().Fail().And.HaveStdOutContaining("MSB3030");

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.dll")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputPath, "SimpleMvc.Views.dll")).Should().NotExist();
        }

        [Fact]
        public void Publish_WithNoBuild_CopiesAlreadyCompiledViews()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            // Build
            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:AssemblyVersion=1.1.1.1").Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            var assemblyPath = Path.Combine(outputPath, "SimpleMvc.dll");
            new FileInfo(assemblyPath).Should().Exist();
            var viewsAssemblyPath = Path.Combine(outputPath, "SimpleMvc.Views.dll");
            new FileInfo(viewsAssemblyPath).Should().Exist();
            var assemblyVersion = AssemblyName.GetAssemblyName(assemblyPath).Version;
            var viewsAssemblyVersion = AssemblyName.GetAssemblyName(viewsAssemblyPath).Version;

            // Publish should copy dlls from OutputPath
            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:NoBuild=true").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            var publishAssemblyPath = Path.Combine(publishOutputPath, "SimpleMvc.dll");
            new FileInfo(publishAssemblyPath).Should().Exist();
            var publishViewsAssemblyPath = Path.Combine(publishOutputPath, "SimpleMvc.Views.dll");
            new FileInfo(publishViewsAssemblyPath).Should().Exist();

            var publishAssemblyVersion = AssemblyName.GetAssemblyName(publishAssemblyPath).Version;
            var publishViewsAssemblyVersion = AssemblyName.GetAssemblyName(publishViewsAssemblyPath).Version;

            Assert.Equal(assemblyVersion, publishAssemblyVersion);
            Assert.Equal(viewsAssemblyVersion, publishViewsAssemblyVersion);
        }
    }
}
