// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Publish.Tests
{
    [TestClass]
    public class GivenThatWeWantToPublishAComServerLibrary : SdkTest
    {
        [TestMethod]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void It_publishes_comhost_to_the_publish_folder()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("ComServer")
                .WithSource();

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute()
                .Should()
                .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework);
            var outputDirectory = new BuildCommand(testAsset).GetOutputDirectory();

            var filesPublished = new[] {
                "ComServer.dll",
                "ComServer.pdb",
                "ComServer.deps.json",
                "ComServer.comhost.dll"
            };

            outputDirectory.Should().HaveFiles(filesPublished);
            publishDirectory.Should().HaveFiles(filesPublished);
        }
    }
}
