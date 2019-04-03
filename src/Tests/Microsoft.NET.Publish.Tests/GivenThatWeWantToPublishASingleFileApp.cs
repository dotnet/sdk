// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishASingleFileApp : SdkTest
    {
        private const string TestProjectName = "HelloWorld";

        public GivenThatWeWantToPublishASingleFileApp(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_errors_when_publishing_single_file_app_without_rid()
        {
             var testAsset = _testAssetsManager
                .CopyTestAsset(TestProjectName)
                .WithSource();

            var publishCommand = new PublishCommand(Log, testAsset.TestRoot);
            publishCommand
                .Execute("/p:SelfContained=true")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotHaveSingleFileWithoutRuntimeIdentifier);
        }

        [Fact]
        public void It_errors_when_publishing_single_file_without_apphost()
        {
            var runtimeIdentifier = RuntimeEnvironment.GetRuntimeIdentifier();

            var testAsset = _testAssetsManager
                .CopyTestAsset(TestProjectName)
                .WithSource();

            var publishCommand = new PublishCommand(Log, testAsset.TestRoot);
            publishCommand
                .Execute(
                    "/p:SelfContained=true",
                    "/p:UseAppHost=false",
                    $"/p:RuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotHaveSingleFileWithoutAppHost);
        }

        private void RunTest(bool isSelfContained, bool includePdb)
        {
            var runtimeIdentifier = RuntimeEnvironment.GetRuntimeIdentifier();
            const string targetFramework = "netcoreapp3.0";

            var testAsset = _testAssetsManager
                .CopyTestAsset(TestProjectName)
                .WithSource();

            var publishCommand = new PublishCommand(Log, testAsset.TestRoot);
            publishCommand
                .Execute(
                    $"/p:TargetFramework={targetFramework}",
                    $"/p:RuntimeIdentifier={runtimeIdentifier}",
                    "/p:SelfContained=" + ((isSelfContained) ? "true" : "false"),
                    "/p:IncludePdbInSingleFile=" + ((includePdb) ? "true" : "false"))
                .Should()
                .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework,
                                                                     runtimeIdentifier: runtimeIdentifier);
            var exeFile = $"{TestProjectName}{Constants.ExeSuffix}";
            var pdbFile = $"{TestProjectName}.pdb";

            string[] expectedFiles = (includePdb) ? new string[] { exeFile } : new string[] { exeFile, pdbFile };
            publishDirectory.Should().OnlyHaveFiles(expectedFiles);
        }

        [Fact]
        public void It_generates_a_single_file_for_framework_dependent_apps()
        {
            RunTest(isSelfContained: false, includePdb: false);
        }

        [Fact]
        public void It_generates_a_single_file_for_self_contained_apps()
        {
            RunTest(isSelfContained: true, includePdb: false);
        }

        [Fact]
        public void It_generates_a_single_file_including_pdbs()
        {
            RunTest(isSelfContained: true, includePdb: true);
        }
    }
}
