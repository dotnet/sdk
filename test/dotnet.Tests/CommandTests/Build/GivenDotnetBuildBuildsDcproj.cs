// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Build.Tests
{
    [TestClass]
    public class GivenDotnetBuildBuildsDcproj : SdkTest
    {
        public GivenDotnetBuildBuildsDcproj()
        {
        }

        [TestMethod]
        public void ItPrintsBuildSummary()
        {
            var testAppName = "docker-compose";
            var testInstance = TestAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .Restore(Log);

            string expectedBuildSummary = @"Build succeeded.
    0 Warning(s)
    0 Error(s)";

            var cmd = new DotnetBuildCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragment(expectedBuildSummary);
        }
    }
}
