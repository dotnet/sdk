// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishASingleFileApp : SdkTest
    {
        private const string TestProjectName = "HelloWorldWithSubDirs";

        private const string PublishSingleFile = "/p:PublishSingleFile=true";
        private const string FrameworkDependent = "/p:SelfContained=false";
        private const string PlaceStamp = "/p:PlaceStamp=true";
        private const string ExcludeNewest = "/p:ExcludeNewest=true";
        private const string ExcludeAlways = "/p:ExcludeAlways=true";
        private const string DontUseAppHost = "/p:UseAppHost=false";
        private const string ReadyToRun = "/p:PublishReadyToRun=true";
        private const string ReadyToRunWithSymbols = "/p:PublishReadyToRunEmitSymbols=true";
        private const string UseAppHost = "/p:UseAppHost=true";
        private const string IncludeDefault = "/p:IncludeSymbolsInSingleFile=false";
        private const string IncludePdb = "/p:IncludeSymbolsInSingleFile=true";
        private const string IncludeNative = "/p:IncludeNativeLibrariesForSelfExtract=true";
        private const string DontIncludeNative = "/p:IncludeNativeLibrariesForSelfExtract=false";
        private const string IncludeAllContent = "/p:IncludeAllContentForSelfExtract=true";

        private readonly string RuntimeIdentifier = $"/p:RuntimeIdentifier={RuntimeInformation.RuntimeIdentifier}";
        private readonly string SingleFile = $"{TestProjectName}{Constants.ExeSuffix}";
        private readonly string PdbFile = $"{TestProjectName}.pdb";
        private const string NewestContent = "Signature.Newest.Stamp";
        private const string AlwaysContent = "Signature.Always.Stamp";

        private const string SmallNameDir = "SmallNameDir";
        private const string LargeNameDir = "This is a directory with a really long name for one that only contains a small file";
        private readonly string SmallNameDirWord = Path.Combine(SmallNameDir, "word").Replace('\\', '/'); // DirectoryInfoAssertions normalizes Path-Separator.
        private readonly string LargeNameDirWord = Path.Combine(SmallNameDir, LargeNameDir, ".word").Replace('\\', '/');

        public GivenThatWeWantToPublishASingleFileApp(ITestOutputHelper log) : base(log)
        {
        }

        private PublishCommand GetPublishCommand()
        {
            var testAsset = _testAssetsManager
               .CopyTestAsset(TestProjectName)
               .WithSource();

            // Create the following content:
            //  <TestRoot>/SmallNameDir/This is a directory with a really long name for one that only contains a small file/.word
            //
            // This content is not checked in to the test assets, but generated during test execution
            // in order to circumvent certain issues like:
            // Git Clone: Cannot clone files with long names on Windows if long file name support is not enabled
            // Nuget Pack: By default ignores files starting with "."
            string longDirPath = Path.Combine(testAsset.TestRoot, SmallNameDir, LargeNameDir);
            Directory.CreateDirectory(longDirPath);
            using (var writer = File.CreateText(Path.Combine(longDirPath, ".word")))
            {
                writer.Write("World!");
            }

            return new PublishCommand(Log, testAsset.TestRoot);
        }

        private string GetNativeDll(string baseName)
        {
            return RuntimeInformation.RuntimeIdentifier.StartsWith("win") ? baseName + ".dll" :
                   RuntimeInformation.RuntimeIdentifier.StartsWith("osx") ? "lib" + baseName + ".dylib" :  "lib" + baseName + ".so";
        }

        private DirectoryInfo GetPublishDirectory(PublishCommand publishCommand, string targetFramework = "net5.0")
        {
            return publishCommand.GetOutputDirectory(targetFramework: targetFramework,
                                                     runtimeIdentifier: RuntimeInformation.RuntimeIdentifier);
        }

        [Fact]
        public void Incremental_add_single_file()
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = "net5.0",
                IsExe = true,
            };
            testProject.AdditionalProperties.Add("SelfContained", $"{true}");

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var cmd = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            var singleFilePath = Path.Combine(GetPublishDirectory(cmd).FullName, $"SingleFileTest{Constants.ExeSuffix}");
            cmd.Execute(RuntimeIdentifier).Should().Pass();
            var time1 = File.GetLastWriteTimeUtc(singleFilePath);

            WaitForUtcNowToAdvance();

            cmd.Execute(PublishSingleFile, RuntimeIdentifier).Should().Pass();
            var time2 = File.GetLastWriteTimeUtc(singleFilePath);

            time2.Should().BeAfter(time1);

            var exeCommand = new RunExeCommand(Log, singleFilePath);
            exeCommand.Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void It_errors_when_publishing_single_file_app_without_rid()
        {
            GetPublishCommand()
                .Execute(PublishSingleFile)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotHaveSingleFileWithoutRuntimeIdentifier);
        }

        [Fact]
        public void It_errors_when_publishing_single_file_without_apphost()
        {
            GetPublishCommand()
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent, DontUseAppHost)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotHaveSingleFileWithoutAppHost);
        }

        [Fact]
        public void It_errors_when_publishing_single_file_lib()
        {
            var testProject = new TestProject()
            {
                Name = "ClassLib",
                TargetFrameworks = "netstandard2.0",
                IsExe = false,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            publishCommand.Execute(PublishSingleFile, RuntimeIdentifier)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotHaveSingleFileWithoutExecutable)
                .And
                .NotHaveStdOutContaining(Strings.CanOnlyHaveSingleFileWithNetCoreApp);
        }

        [Fact]
        public void It_errors_when_targetting_netstandard()
        {
            var testProject = new TestProject()
            {
                Name = "NetStandardExe",
                TargetFrameworks = "netstandard2.0",
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            publishCommand.Execute(PublishSingleFile, RuntimeIdentifier, UseAppHost)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CanOnlyHaveSingleFileWithNetCoreApp)
                .And
                .NotHaveStdOutContaining(Strings.CannotHaveSingleFileWithoutExecutable);
        }

        [Fact]
        public void It_errors_when_targetting_netcoreapp_2_x()
        {
            var testProject = new TestProject()
            {
                Name = "ConsoleApp",
                TargetFrameworks = "netcoreapp2.2",
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            publishCommand.Execute(PublishSingleFile, RuntimeIdentifier)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.PublishSingleFileRequiresVersion30);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_errors_when_including_all_content_but_not_native_libraries()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeAllContent, DontIncludeNative)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotIncludeAllContentButNotNativeLibrariesInSingleFile);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_generates_a_single_file_for_framework_dependent_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile, SmallNameDirWord, LargeNameDirWord };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_generates_a_single_file_for_self_contained_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile, SmallNameDirWord, LargeNameDirWord };
            string[] unexpectedFiles = { GetNativeDll("hostfxr"), GetNativeDll("hostpolicy") };

            GetPublishDirectory(publishCommand)
                .Should()
                .HaveFiles(expectedFiles)
                .And
                .NotHaveFiles(unexpectedFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_generates_a_single_file_with_native_binaries_for_framework_dependent_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent, IncludeNative)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile, SmallNameDirWord, LargeNameDirWord };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_generates_a_single_file_with_native_binaries_for_self_contained_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeNative)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile, SmallNameDirWord, LargeNameDirWord };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_generates_a_single_file_with_all_content_for_framework_dependent_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent, IncludeAllContent)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_generates_a_single_file_with_all_content_for_self_contained_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeAllContent)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionTheory("16.8.0")]
        [InlineData("netcoreapp3.0")]
        [InlineData("netcoreapp3.1")]
        public void It_generates_a_single_file_including_pdbs(string targetFramework)
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = targetFramework,
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeAllContent, IncludePdb)
                .Should()
                .Pass();

            string[] expectedFiles = { $"{testProject.Name}{Constants.ExeSuffix}" };
            GetPublishDirectory(publishCommand, targetFramework)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_excludes_ni_pdbs_from_single_file()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // R2R doesn't produce ni pdbs on OSX.
                return;
            }

            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeAllContent, ReadyToRun, ReadyToRunWithSymbols)
                .Should()
                .Pass();

            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: "net5.0", runtimeIdentifier: RuntimeInformation.RuntimeIdentifier);
            var mainProjectDll = Path.Combine(intermediateDirectory.FullName, $"{TestProjectName}.dll");
            var niPdbFile = GivenThatWeWantToPublishReadyToRun.GetPDBFileName(mainProjectDll);

            string[] expectedFiles = { SingleFile, PdbFile, niPdbFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionTheory("16.8.0")]
        [InlineData("netcoreapp3.0")]
        [InlineData("netcoreapp3.1")]
        public void It_can_include_ni_pdbs_in_single_file(string targetFramework)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // R2R doesn't produce ni pdbs on OSX.
                return;
            }

            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = targetFramework,
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, ReadyToRun, ReadyToRunWithSymbols, IncludeAllContent, IncludePdb)
                .Should()
                .Pass();

            string[] expectedFiles = { $"{testProject.Name}{Constants.ExeSuffix}" };
            GetPublishDirectory(publishCommand, targetFramework)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionTheory("16.8.0")]
        [InlineData(ExcludeNewest, NewestContent)]
        [InlineData(ExcludeAlways, AlwaysContent)]
        public void It_generates_a_single_file_excluding_content(string exclusion, string content)
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeAllContent, PlaceStamp, exclusion)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile, content };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_generates_a_single_file_for_R2R_compiled_Apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeAllContent, ReadyToRun)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_does_not_rewrite_the_single_file_unnecessarily()
        {
            var publishCommand = GetPublishCommand();
            var singleFilePath = Path.Combine(GetPublishDirectory(publishCommand).FullName, SingleFile);

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            DateTime fileWriteTimeAfterFirstRun = File.GetLastWriteTimeUtc(singleFilePath);

            WaitForUtcNowToAdvance();

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            DateTime fileWriteTimeAfterSecondRun = File.GetLastWriteTimeUtc(singleFilePath);

            fileWriteTimeAfterSecondRun.Should().Be(fileWriteTimeAfterFirstRun);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_rewrites_the_apphost_for_single_file_publish()
        {
            var publishCommand = GetPublishCommand();
            var appHostPath = Path.Combine(GetPublishDirectory(publishCommand).FullName, SingleFile);
            var singleFilePath = appHostPath;

            publishCommand
                .Execute(RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            var appHostSize = new FileInfo(appHostPath).Length;

            WaitForUtcNowToAdvance();

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            var singleFileSize = new FileInfo(singleFilePath).Length;

            singleFileSize.Should().BeGreaterThan(appHostSize);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_rewrites_the_apphost_for_non_single_file_publish()
        {
            var publishCommand = GetPublishCommand();
            var appHostPath = Path.Combine(GetPublishDirectory(publishCommand).FullName, SingleFile);
            var singleFilePath = appHostPath;

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            var singleFileSize = new FileInfo(singleFilePath).Length;

            WaitForUtcNowToAdvance();

            publishCommand
                .Execute(RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            var appHostSize = new FileInfo(appHostPath).Length;

            appHostSize.Should().BeLessThan(singleFileSize);
        }

        [RequiresMSBuildVersionTheory("16.8.0")]
        [InlineData("netcoreapp3.0", false, IncludeDefault)]
        [InlineData("netcoreapp3.0", true, IncludeDefault)]
        [InlineData("netcoreapp3.0", false, IncludePdb)]
        [InlineData("netcoreapp3.0", true, IncludePdb)]
        [InlineData("netcoreapp3.1", false, IncludeDefault)]
        [InlineData("netcoreapp3.1", true, IncludeDefault)]
        [InlineData("netcoreapp3.1", false, IncludePdb)]
        [InlineData("netcoreapp3.1", true, IncludePdb)]
        [InlineData("net5.0", false, IncludeDefault)]
        [InlineData("net5.0", false, IncludeNative)]
        [InlineData("net5.0", false, IncludeAllContent)]
        [InlineData("net5.0", true, IncludeDefault)]
        [InlineData("net5.0", true, IncludeNative)]
        [InlineData("net5.0", true, IncludeAllContent)]
        public void It_runs_single_file_apps(string targetFramework, bool selfContained, string bundleOption)
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = targetFramework,
                IsExe = true,
            };
            testProject.AdditionalProperties.Add("SelfContained", $"{selfContained}");

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            publishCommand.Execute(PublishSingleFile, RuntimeIdentifier, bundleOption)
                .Should()
                .Pass();

            var publishDir = GetPublishDirectory(publishCommand, targetFramework).FullName;
            var singleFilePath = Path.Combine(publishDir, $"{testProject.Name}{Constants.ExeSuffix}");

            var command = new RunExeCommand(Log, singleFilePath);
            command.Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World");
        }

        [RequiresMSBuildVersionTheory("16.8.0")]
        [InlineData(false)]
        [InlineData(true)]
        public void It_errors_when_including_symbols_targeting_net5(bool selfContained)
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = "net5.0",
                IsExe = true,
            };
            testProject.AdditionalProperties.Add("SelfContained", $"{selfContained}");

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            publishCommand.Execute(PublishSingleFile, RuntimeIdentifier, IncludePdb)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotIncludeSymbolsInSingleFile);
        }
    }
}
