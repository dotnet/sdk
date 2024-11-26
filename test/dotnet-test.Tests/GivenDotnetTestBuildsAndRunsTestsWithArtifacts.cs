// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestsWithArtifacts : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsTestsWithArtifacts(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void RunTestProjectWithFailingTestsAndFileArtifacts_ShouldReturnOneAsExitCode()
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectSolutionWithTestsAndArtifacts", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute();

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(@".*Test6.*testNodeFile.txt", result.StdOut);

                result.StdOut
                    .Should().Contain("In process file artifacts")
                    .And.Contain("file.txt")
                    .And.Contain("sessionFile.txt");

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 6")
                    .And.Contain("succeeded: 1")
                    .And.Contain("failed: 4")
                    .And.Contain("skipped: 1");
            }

            result.ExitCode.Should().Be(1);
        }
    }
}
