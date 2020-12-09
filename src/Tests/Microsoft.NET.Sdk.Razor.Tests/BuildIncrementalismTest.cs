// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
    public class BuildIncrementalismTest : SdkTest
    {
        public BuildIncrementalismTest(ITestOutputHelper log) : base(log) {}

        // [ConditionalFact]
        // [OSSkipCondition(OperatingSystems.MacOSX | OperatingSystems.Linux, SkipReason = "See https://github.com/aspnet/Razor/issues/2219")]
        // [InitializeTestProject("SimpleMvc")]
        // public async Task BuildIncremental_SimpleMvc_PersistsTargetInputFile()
        // {
        //     // Arrange
        //     var thumbprintLookup = new Dictionary<string, FileThumbPrint>();

        //     // Act 1
        //     var result = await DotnetMSBuild("Build");

        //     var directoryPath = Path.Combine(result.Project.DirectoryPath, IntermediateOutputPath);
        //     var filesToIgnore = new[]
        //     {
        //         // These files are generated on every build.
        //         Path.Combine(directoryPath, "SimpleMvc.csproj.CopyComplete"),
        //         Path.Combine(directoryPath, "SimpleMvc.csproj.FileListAbsolute.txt"),
        //     };

        //     var files = Directory.GetFiles(directoryPath).Where(p => !filesToIgnore.Contains(p));
        //     foreach (var file in files)
        //     {
        //         var thumbprint = GetThumbPrint(file);
        //         thumbprintLookup[file] = thumbprint;
        //     }

        //     // Assert 1
        //     Assert.BuildPassed(result);

        //     // Act & Assert 2
        //     for (var i = 0; i < 2; i++)
        //     {
        //         // We want to make sure nothing changed between multiple incremental builds.
        //         using (var razorGenDirectoryLock = LockDirectory(RazorIntermediateOutputPath))
        //         {
        //             result = await DotnetMSBuild("Build");
        //         }

        //         Assert.BuildPassed(result);
        //         foreach (var file in files)
        //         {
        //             var thumbprint = GetThumbPrint(file);
        //             Assert.Equal(thumbprintLookup[file], thumbprint);
        //         }
        //     }
        // }

        // [Fact]
        // [InitializeTestProject("SimpleMvc")]
        // public async Task RazorGenerate_RegeneratesTagHelperInputs_IfFileChanges()
        // {
        //     // Act - 1
        //     var expectedTagHelperCacheContent = @"""Name"":""SimpleMvc.SimpleTagHelper""";
        //     var result = await DotnetMSBuild("Build");
        //     var file = Path.Combine(Project.DirectoryPath, "SimpleTagHelper.cs");
        //     var tagHelperOutputCache = Path.Combine(IntermediateOutputPath, "SimpleMvc.TagHelpers.output.cache");
        //     var generatedFile = Path.Combine(RazorIntermediateOutputPath, "Views", "Home", "Index.cshtml.g.cs");

        //     // Assert - 1
        //     Assert.BuildPassed(result);
        //     Assert.FileContains(result, tagHelperOutputCache, expectedTagHelperCacheContent);
        //     var fileThumbPrint = GetThumbPrint(generatedFile);

        //     // Act - 2
        //     // Update the source content and build. We should expect the outputs to be regenerated.
        //     ReplaceContent(string.Empty, file);
        //     result = await DotnetMSBuild("Build");

        //     // Assert - 2
        //     Assert.BuildPassed(result);
        //     Assert.FileDoesNotContain(result, tagHelperOutputCache, @"""Name"":""SimpleMvc.SimpleTagHelper""");
        //     var newThumbPrint = GetThumbPrint(generatedFile);
        //     Assert.NotEqual(fileThumbPrint, newThumbPrint);
        // }

        // [Fact]
        // [InitializeTestProject("SimpleMvc")]
        // public async Task Build_ErrorInGeneratedCode_ReportsMSBuildError_OnIncrementalBuild()
        // {
        //     // Introducing a Razor semantic error
        //     ReplaceContent("@{ // Unterminated code block", "Views", "Home", "Index.cshtml");

        //     // Regular build
        //     await VerifyError();

        //     // Incremental build
        //     await VerifyError();

        //     async Task VerifyError()
        //     {
        //         var result = await DotnetMSBuild("Build");

        //         Assert.BuildFailed(result);

        //         var filePath = Path.Combine(Project.DirectoryPath, "Views", "Home", "Index.cshtml");
        //         var location = filePath + "(1,2)";
        //         if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        //         {
        //             // Absolute paths on OSX don't work well.
        //             location = null;
        //         }

        //         Assert.BuildError(result, "RZ1006", location: location);

        //         // Compilation failed without creating the views assembly
        //         Assert.FileExists(result, IntermediateOutputPath, "SimpleMvc.dll");
        //         Assert.FileDoesNotExist(result, IntermediateOutputPath, "SimpleMvc.Views.dll");

        //         // File with error does not get written to disk.
        //         Assert.FileDoesNotExist(result, IntermediateOutputPath, "Razor", "Views", "Home", "Index.cshtml.g.cs");
        //     }
        // }

        // [Fact]
        // [InitializeTestProject("MvcWithComponents")]
        // public async Task BuildComponents_ErrorInGeneratedCode_ReportsMSBuildError_OnIncrementalBuild()
        // {
        //     // Introducing a Razor semantic error
        //     ReplaceContent("@{ // Unterminated code block", "Views", "Shared", "NavMenu.razor");

        //     // Regular build
        //     await VerifyError();

        //     // Incremental build
        //     await VerifyError();

        //     async Task VerifyError()
        //     {
        //         var result = await DotnetMSBuild("Build");

        //         Assert.BuildFailed(result);

        //         // This needs to be relative path. Tracked by https://github.com/aspnet/Razor/issues/2187.
        //         var filePath = Path.Combine(Project.DirectoryPath, "Views", "Shared", "NavMenu.razor");
        //         var location = filePath + "(1,2)";
        //         if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        //         {
        //             // Absolute paths on OSX don't work well.
        //             location = null;
        //         }

        //         Assert.BuildError(result, "RZ1006", location: location);

        //         // Compilation failed without creating the views assembly
        //         Assert.FileDoesNotExist(result, IntermediateOutputPath, "MvcWithComponents.dll");
        //         Assert.FileDoesNotExist(result, IntermediateOutputPath, "MvcWithComponents.Views.dll");

        //         // File with error does not get written to disk.
        //         Assert.FileDoesNotExist(result, IntermediateOutputPath, "RazorComponents", "Views", "Shared", "NavMenu.razor.g.cs");
        //     }
        // }

        // [Fact]
        // [InitializeTestProject("MvcWithComponents")]
        // public async Task BuildComponents_DoesNotRegenerateComponentDefinition_WhenDefinitionIsUnchanged()
        // {
        //     // Act - 1
        //     var updatedContent = "Some content";
        //     var tagHelperOutputCache = Path.Combine(IntermediateOutputPath, "MvcWithComponents.TagHelpers.output.cache");

        //     var generatedFile = Path.Combine(RazorIntermediateOutputPath, "Views", "Shared", "NavMenu.razor.g.cs");
        //     var generatedDefinitionFile = Path.Combine(RazorComponentIntermediateOutputPath, "Views", "Shared", "NavMenu.razor.g.cs");

        //     // Assert - 1
        //     var result = await DotnetMSBuild("Build");

        //     Assert.BuildPassed(result);
        //     var outputFile = Path.Combine(OutputPath, "MvcWithComponents.dll");
        //     Assert.FileExists(result, OutputPath, "MvcWithComponents.dll");
        //     var outputAssemblyThumbprint = GetThumbPrint(outputFile);

        //     Assert.FileExists(result, generatedDefinitionFile);
        //     var generatedDefinitionThumbprint = GetThumbPrint(generatedDefinitionFile);
        //     Assert.FileExists(result, generatedFile);
        //     var generatedFileThumbprint = GetThumbPrint(generatedFile);

        //     Assert.FileExists(result, tagHelperOutputCache);
        //     Assert.FileContains(
        //         result,
        //         tagHelperOutputCache,
        //         @"""Name"":""MvcWithComponents.Views.Shared.NavMenu""");

        //     var definitionThumbprint = GetThumbPrint(tagHelperOutputCache);

        //     // Act - 2
        //     ReplaceContent(updatedContent, "Views", "Shared", "NavMenu.razor");
        //     result = await DotnetMSBuild("Build");

        //     // Assert - 2
        //     Assert.FileExists(result, generatedDefinitionFile);
        //     // Definition file remains unchanged.
        //     Assert.Equal(generatedDefinitionThumbprint, GetThumbPrint(generatedDefinitionFile));
        //     Assert.FileExists(result, generatedFile);
        //     // Generated file should change and include the new content.
        //     Assert.NotEqual(generatedFileThumbprint, GetThumbPrint(generatedFile));
        //     Assert.FileContains(result, generatedFile, updatedContent);

        //     // TagHelper cache should remain unchanged.
        //     Assert.Equal(definitionThumbprint, GetThumbPrint(tagHelperOutputCache));
        // }

        // [Fact]
        // [InitializeTestProject("MvcWithComponents")]
        // public async Task BuildComponents_RegeneratesComponentDefinition_WhenFilesChange()
        // {
        //     // Act - 1
        //     var updatedContent = "@code { [Parameter] public string AParameter { get; set; } }";
        //     var tagHelperOutputCache = Path.Combine(IntermediateOutputPath, "MvcWithComponents.TagHelpers.output.cache");

        //     var generatedFile = Path.Combine(RazorIntermediateOutputPath, "Views", "Shared", "NavMenu.razor.g.cs");
        //     var generatedDefinitionFile = Path.Combine(RazorComponentIntermediateOutputPath, "Views", "Shared", "NavMenu.razor.g.cs");

        //     // Assert - 1
        //     var result = await DotnetMSBuild("Build");

        //     Assert.BuildPassed(result);
        //     var outputFile = Path.Combine(OutputPath, "MvcWithComponents.dll");
        //     Assert.FileExists(result, OutputPath, "MvcWithComponents.dll");
        //     var outputAssemblyThumbprint = GetThumbPrint(outputFile);

        //     Assert.FileExists(result, generatedDefinitionFile);
        //     var generatedDefinitionThumbprint = GetThumbPrint(generatedDefinitionFile);
        //     Assert.FileExists(result, generatedFile);
        //     var generatedFileThumbprint = GetThumbPrint(generatedFile);

        //     Assert.FileExists(result, tagHelperOutputCache);
        //     Assert.FileContains(
        //         result,
        //         tagHelperOutputCache,
        //         @"""Name"":""MvcWithComponents.Views.Shared.NavMenu""");

        //     var definitionThumbprint = GetThumbPrint(tagHelperOutputCache);

        //     // Act - 2
        //     ReplaceContent(updatedContent, "Views", "Shared", "NavMenu.razor");
        //     result = await DotnetMSBuild("Build");

        //     // Assert - 2
        //     Assert.FileExists(result, OutputPath, "MvcWithComponents.dll");
        //     Assert.NotEqual(outputAssemblyThumbprint, GetThumbPrint(outputFile));

        //     Assert.FileExists(result, generatedDefinitionFile);
        //     Assert.NotEqual(generatedDefinitionThumbprint, GetThumbPrint(generatedDefinitionFile));
        //     Assert.FileExists(result, generatedFile);
        //     Assert.NotEqual(generatedFileThumbprint, GetThumbPrint(generatedFile));

        //     Assert.FileExists(result, tagHelperOutputCache);
        //     Assert.FileContains(
        //         result,
        //         tagHelperOutputCache,
        //         @"""Name"":""MvcWithComponents.Views.Shared.NavMenu""");

        //     Assert.FileContains(
        //         result,
        //         tagHelperOutputCache,
        //         "AParameter");

        //     Assert.NotEqual(definitionThumbprint, GetThumbPrint(tagHelperOutputCache));
        // }

        // [Fact]
        // [InitializeTestProject("MvcWithComponents")]
        // public async Task BuildComponents_DoesNotModifyFiles_IfFilesDoNotChange()
        // {
        //     // Act - 1
        //     var tagHelperOutputCache = Path.Combine(IntermediateOutputPath, "MvcWithComponents.TagHelpers.output.cache");

        //     var file = Path.Combine(Project.DirectoryPath, "Views", "Shared", "NavMenu.razor.g.cs");
        //     var generatedFile = Path.Combine(RazorIntermediateOutputPath, "Views", "Shared", "NavMenu.razor.g.cs");
        //     var generatedDefinitionFile = Path.Combine(RazorComponentIntermediateOutputPath, "Views", "Shared", "NavMenu.razor.g.cs");

        //     // Assert - 1
        //     var result = await DotnetMSBuild("Build");

        //     Assert.BuildPassed(result);
        //     var outputFile = Path.Combine(OutputPath, "MvcWithComponents.dll");
        //     Assert.FileExists(result, OutputPath, "MvcWithComponents.dll");
        //     var outputAssemblyThumbprint = GetThumbPrint(outputFile);

        //     Assert.FileExists(result, generatedDefinitionFile);
        //     var generatedDefinitionThumbprint = GetThumbPrint(generatedDefinitionFile);
        //     Assert.FileExists(result, generatedFile);
        //     var generatedFileThumbprint = GetThumbPrint(generatedFile);

        //     Assert.FileExists(result, tagHelperOutputCache);
        //     Assert.FileContains(
        //         result,
        //         tagHelperOutputCache,
        //         @"""Name"":""MvcWithComponents.Views.Shared.NavMenu""");

        //     var definitionThumbprint = GetThumbPrint(tagHelperOutputCache);

        //     // Act - 2
        //     result = await DotnetMSBuild("Build");

        //     // Assert - 2
        //     Assert.FileExists(result, OutputPath, "MvcWithComponents.dll");
        //     Assert.Equal(outputAssemblyThumbprint, GetThumbPrint(outputFile));

        //     Assert.FileExists(result, generatedDefinitionFile);
        //     Assert.Equal(generatedDefinitionThumbprint, GetThumbPrint(generatedDefinitionFile));
        //     Assert.FileExists(result, generatedFile);
        //     Assert.Equal(generatedFileThumbprint, GetThumbPrint(generatedFile));

        //     Assert.FileExists(result, tagHelperOutputCache);
        //     Assert.FileContains(
        //         result,
        //         tagHelperOutputCache,
        //         @"""Name"":""MvcWithComponents.Views.Shared.NavMenu""");

        //     Assert.Equal(definitionThumbprint, GetThumbPrint(tagHelperOutputCache));
        // }

        [Fact]
        public void IncrementalBuild_WithP2P_WorksWhenBuildProjectReferencesIsDisabled()
        {
            // Simulates building the same way VS does by setting BuildProjectReferences=false.
            // With this flag, the only target called is GetCopyToOutputDirectoryItems on the referenced project.
            // We need to ensure that we continue providing Razor binaries and symbols as files to be copied over.
            var testAsset = "AppWithP2PReference";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();
            
            var build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute().Should().Pass();

            string outputPath = build.GetOutputDirectory("net5.0").FullName;

            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.pdb")).Should().Exist();

            var clean = new MSBuildCommand(Log, "Clean", build.FullPathProjectFile);
            clean.Execute("/p:BuildProjectReferences=false").Should().Pass();

            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.pdb")).Should().NotExist();

            // dotnet msbuild /p:BuildProjectReferences=false
            build = new BuildCommand(projectDirectory, "AppWithP2PReference");
            build.Execute("/p:BuildProjectReferences=false").Should().Pass();

            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "AppWithP2PReference.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.pdb")).Should().Exist();
        }

        [Fact]
        public void Build_TouchesUpToDateMarkerFile()
        {
            var testAsset = "ClassLibrary";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            // Remove the components so that they don't interfere with these tests
            Directory.Delete(Path.Combine(projectDirectory.Path, "Components"), recursive: true);

            var build = new BuildCommand(projectDirectory);
            build.Execute()
                .Should()
                .Pass();

            string intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().FullName, "Debug", "net5.0");

            var classLibraryDll = Path.Combine(intermediateOutputPath, "ClassLibrary.dll");
            var classLibraryViewsDll = Path.Combine(intermediateOutputPath, "ClassLibrary.Views.dll");
            var markerFile = Path.Combine(intermediateOutputPath, "ClassLibrary.csproj.CopyComplete");;

            new FileInfo(classLibraryDll).Should().Exist();
            new FileInfo(classLibraryViewsDll).Should().Exist();
            new FileInfo(markerFile).Should().Exist();

            // Gather thumbprints before incremental build.
            var classLibraryThumbPrint = FileThumbPrint.Create(classLibraryDll);
            var classLibraryViewsThumbPrint = FileThumbPrint.Create(classLibraryViewsDll);
            var markerFileThumbPrint = FileThumbPrint.Create(markerFile);

            build = new BuildCommand(projectDirectory);
            build.Execute()
                .Should()
                .Pass();

            // Verify thumbprint file is unchanged between true incremental builds
            Assert.Equal(classLibraryThumbPrint, FileThumbPrint.Create(classLibraryDll));
            Assert.Equal(classLibraryViewsThumbPrint, FileThumbPrint.Create(classLibraryViewsDll));
            // In practice, this should remain unchanged. However, since our tests reference
            // binaries from other projects, this file gets updated by Microsoft.Common.targets
            Assert.NotEqual(markerFileThumbPrint, FileThumbPrint.Create(markerFile));

            // Change a cshtml file and verify ClassLibrary.Views.dll and marker file are updated
            File.AppendAllText(Path.Combine(projectDirectory.Path, "Views", "_ViewImports.cshtml"), Environment.NewLine);

            build = new BuildCommand(projectDirectory);
            build.Execute()
                .Should()
                .Pass();

            Assert.Equal(classLibraryThumbPrint, FileThumbPrint.Create(classLibraryDll));
            Assert.NotEqual(classLibraryViewsThumbPrint, FileThumbPrint.Create(classLibraryViewsDll));
            Assert.NotEqual(markerFileThumbPrint, FileThumbPrint.Create(markerFile));
        }
    }
}
