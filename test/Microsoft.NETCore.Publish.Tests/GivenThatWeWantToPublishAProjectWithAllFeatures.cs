// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NETCore.TestFramework;
using Microsoft.NETCore.TestFramework.Assertions;
using Microsoft.NETCore.TestFramework.Commands;
using Xunit;
using static Microsoft.NETCore.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NETCore.Publish.Tests
{
    public class GivenThatWeWantToPublishAProjectWithAllFeatures
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        //[Fact]
        public void It_publishes_the_project_correctly()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("KitchenSink")
                .WithSource()
                .Restore("--fallbacksource", $"{RepoInfo.PackagesPath}");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            PublishCommand publishCommand = new PublishCommand(Stage0MSBuild, appProjectDirectory);
            publishCommand
                .Execute()
                .Should()
                .Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory();

            publishDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.dll",
                "TestApp.pdb",
                "TestApp.deps.json",
                "TestApp.runtimeconfig.json",
                "TestLibrary.dll",
                "TestLibrary.pdb",
                "CompileCopyToOutput.cs",
                "Resource1.resx",
                "ContentAlways.txt",
                "ContentPreserveNewest.txt",
                "NoneCopyOutputAlways.txt",
                "NoneCopyOutputPreserveNewest.txt",
                "CopyToOutputFromProjectReference.txt",
            });
        }
    }
}