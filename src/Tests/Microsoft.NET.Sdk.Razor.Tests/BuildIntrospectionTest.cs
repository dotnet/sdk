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
    public class BuildIntrospectionTest : SdkTest
    {
        public BuildIntrospectionTest(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void RazorSdk_AddsProjectCapabilities()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectProjectCapabilityItems")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("ProjectCapability: DotNetCoreRazor")
                .And.HaveStdOutContaining("ProjectCapability: DotNetCoreRazorConfiguration");
        }

        [Fact]
        public void RazorSdk_AddsCshtmlFilesToUpToDateCheckInput()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectUpToDateCheck")
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"UpToDateCheckInput: {Path.Combine("Views", "Home", "Index.cshtml")}")
                .And.HaveStdOutContaining($"UpToDateCheckInput: {Path.Combine("Views", "_ViewStart.cshtml")}")
                .And.HaveStdOutContaining($"UpToDateCheckBuilt: {Path.Combine("obj", "Debug", "net5.0", "SimpleMvc.Views.dll")}");
        }

        [Fact]
        public void RazorSdk_AddsGeneratedRazorFilesAndAssemblyInfoToRazorCompile()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectRazorCompileItems")
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"RazorCompile: {Path.Combine("obj", "Debug", "net5.0", "Razor", "Views", "Home", "Index.cshtml.g.cs")}")
                .And.HaveStdOutContaining($"RazorCompile: {Path.Combine("obj", "Debug", "net5.0", "SimpleMvc.RazorTargetAssemblyInfo.cs")}");
        }

        [Fact]
        public void RazorSdk_UsesUseSharedCompilationToSetDefaultValueOfUseRazorBuildServer()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:UseSharedCompilation=false", "/t:_IntrospectUseRazorBuildServer")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("UseRazorBuildServer: false");
        }

        [Fact]
        public void  GetCopyToOutputDirectoryItems_WhenNoFileIsPresent_ReturnsEmptySequence()
        {
            var testAsset = "ClassLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            string outputPath = build.GetOutputDirectory("net5.0").ToString();

            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().Exist();

            var command = new MSBuildCommand(Log, "GetCopyToOutputDirectoryItems", projectDirectory.Path);
            build.Execute("/t:_IntrospectGetCopyToOutputDirectoryItems", "/p:BuildProjectReferences=false")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("AllItemsFullPathWithTargetPath: ClassLibrary.Views.dll")
                .And.HaveStdOutContaining("AllItemsFullPathWithTargetPath: ClassLibrary.Views.pdb");

            // Remove all views from the class library
            Directory.Delete(Path.Combine(projectDirectory.Path, "Views"), recursive: true);

            // dotnet msbuild /p:BuildProjectReferences=false
            build.Execute("/t:_IntrospectGetCopyToOutputDirectoryItems", "/p:BuildProjectReferences=false")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("AllItemsFullPathWithTargetPath: ClassLibrary.Views.dll")
                .And.NotHaveStdOutContaining("AllItemsFullPathWithTargetPath: ClassLibrary.Views.pdb");
        }

        [Fact]
        public void RazorSdk_ResolvesRazorLangVersionTo30ForNetCoreApp30Projects()
        {
            var testAsset = "SimpleMvc31";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new MSBuildCommand(Log, "ResolveRazorConfiguration", projectDirectory.Path);
            build.Execute("/t:_IntrospectResolvedConfiguration")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("RazorLangVersion: 3.0")
                .And.HaveStdOutContaining("ResolvedRazorConfiguration: MVC-3.0");
        }

        [Fact]
        public void RazorSdk_ResolvesRazorLangVersionTo50ForNetCoreApp50Projects()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new MSBuildCommand(Log, "ResolveRazorConfiguration", projectDirectory.Path);
            build.Execute("/t:_IntrospectResolvedConfiguration")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("RazorLangVersion: 5.0")
                .And.HaveStdOutContaining("ResolvedRazorConfiguration: MVC-3.0");
        }

        [Fact]
        public void RazorSdk_ResolvesRazorLangVersionFromValueSpecified()
        {
            var testAsset = "ComponentLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new MSBuildCommand(Log, "ResolveRazorConfiguration", projectDirectory.Path);
            build.Execute("/t:_IntrospectResolvedConfiguration")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("RazorLangVersion: 3.0")
                .And.HaveStdOutContaining("ResolvedRazorConfiguration: Default");
        }

        [Fact]
        public void RazorSdk_ResolvesRazorLangVersionTo21WhenUnspecified()
        {
            var testAsset = "ComponentLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    p.Root.Add(propertyGroup);

                    propertyGroup.Add(new XElement(ns + "RazorLangVersion"));
                });

            var build = new MSBuildCommand(Log, "ResolveRazorConfiguration", projectDirectory.Path);
            build.Execute("/t:_IntrospectResolvedConfiguration")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("RazorLangVersion: 2.1")
                .And.HaveStdOutContaining("ResolvedRazorConfiguration:");
        }

        [Fact]
        public void RazorSdk_WithRazorLangVersionLatest()
        {
            var testAsset = "ComponentLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new MSBuildCommand(Log, "ResolveRazorConfiguration", projectDirectory.Path);
            build.Execute("/t:_IntrospectResolvedConfiguration", "/p:RazorLangVersion=Latest")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("RazorLangVersion: Latest")
                .And.HaveStdOutContaining("ResolvedRazorConfiguration: Default");
        }

        [Fact]
        public void RazorSdk_ResolvesRazorConfigurationToMvc30()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new MSBuildCommand(Log, "ResolveRazorConfiguration", projectDirectory.Path);
            build.Execute("/t:_IntrospectResolvedConfiguration")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("RazorLangVersion: 5.0")
                .And.HaveStdOutContaining("ResolvedRazorConfiguration: MVC-3.0");
        }

        [Fact]
        public void UpToDateReloadFileTypes_Default()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectUpToDateReloadFileTypes")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("UpToDateReloadFileTypes: ;.cs;.razor;.resx;.cshtml");
        }

        [Fact]
        public void UpToDateReloadFileTypes_WithRuntimeCompilation()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    p.Root.Add(propertyGroup);

                    propertyGroup.Add(new XElement(ns + "RazorUpToDateReloadFileTypes", @"$(RazorUpToDateReloadFileTypes.Replace('.cshtml', ''))"));
                });

            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectUpToDateReloadFileTypes")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("UpToDateReloadFileTypes: ;.cs;.razor;.resx;");
        }

        [Fact]
        public void UpToDateReloadFileTypes_WithwWorkAroundRemoved()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectUpToDateReloadFileTypes")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("UpToDateReloadFileTypes: ;.cs;.razor;.resx;.cshtml");
        }

        [Fact]
        public void UpToDateReloadFileTypes_WithRuntimeCompilationAndWorkaroundRemoved()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    p.Root.Add(propertyGroup);

                    propertyGroup.Add(new XElement(ns + "RazorUpToDateReloadFileTypes", @"$(RazorUpToDateReloadFileTypes.Replace('.cshtml', ''))"));
                });

            var build = new BuildCommand(projectDirectory);
            build.Execute("/t:_IntrospectUpToDateReloadFileTypes", "/p:_RazorUpToDateReloadFileTypesAllowWorkaround=false")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("UpToDateReloadFileTypes: ;.cs;.razor;.resx;");
        }

        [Fact]
        public void IntrospectJsonContentFiles()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new MSBuildCommand(Log, "_IntrospectContentItems", projectDirectory.Path);
            build.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining("Content: appsettings.json CopyToOutputDirectory=PreserveNewest CopyToPublishDirectory=PreserveNewest ExcludeFromSingleFile=true")
                .And.HaveStdOutContaining("Content: appsettings.Development.json CopyToOutputDirectory=PreserveNewest CopyToPublishDirectory=PreserveNewest ExcludeFromSingleFile=true");
        }

        [Fact]
        public void IntrospectJsonContentFiles_WithExcludeConfigFilesFromBuildOutputSet()
        {
            // Verifies that the fix for https://github.com/dotnet/aspnetcore/issues/14017 works.
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new MSBuildCommand(Log, "_IntrospectContentItems", projectDirectory.Path);
            build.Execute("/p:ExcludeConfigFilesFromBuildOutput=true")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("Content: appsettings.json CopyToOutputDirectory= CopyToPublishDirectory=PreserveNewest ExcludeFromSingleFile=true")
                .And.HaveStdOutContaining("Content: appsettings.Development.json CopyToOutputDirectory= CopyToPublishDirectory=PreserveNewest ExcludeFromSingleFile=true");
        }

        [Fact]
        public void IntrospectRazorTasksDllPath()
        {
            // Regression test for https://github.com/dotnet/aspnetcore/issues/17308
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new MSBuildCommand(Log, "_IntrospectRazorTasks", projectDirectory.Path);
            build.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"RazorTasksPath:")
                .And.HaveStdOutContaining(TestContext.Current.ToolsetUnderTest.SdksPath)
                .And.HaveStdOutContaining("Microsoft.NET.Sdk.Razor.Tasks.dll");
        }

        [FullMSBuildOnlyFact]
        public void IntrospectRazorTasksDllPath_DesktopMsBuild()
        {
            var testAsset = "SimpleMvc";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            // Verifies the fix for https://github.com/dotnet/aspnetcore/issues/17308
            var build = new MSBuildCommand(Log, "_IntrospectRazorTasks", projectDirectory.Path);
            build.Execute()
                .Should().Pass()
                .And.HaveStdOutContaining(TestContext.Current.ToolsetUnderTest.SdksPath)
                .And.HaveStdOutContaining("Microsoft.NET.Sdk.Razor.Tasks.dll");;
        }

        [Fact]
        public void IntrospectRazorSdkWatchItems()
        {
            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new MSBuildCommand(Log, "_IntrospectWatchItems", projectDirectory.Path);
            build.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining("Watch: Index.razor")
                .And.HaveStdOutContaining("Watch: Index.razor.css");
        }
    }
}
