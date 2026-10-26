// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace EndToEnd.Tests
{
    public class VersionTests(ITestOutputHelper log) : SdkTest(log)
    {
        [Fact]
        public void DotnetVersionReturnsCorrectVersion()
        {
            var result = new DotnetCommand(Log).Execute("--version");
            result.Should().Pass();

            var dotnetFolder = Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath);
            var sdkFolders = Directory.GetDirectories(Path.Combine(dotnetFolder, "sdk"));
            sdkFolders.Length.Should().Be(1, "Only one SDK folder is expected in the layout");

            var expectedSdkVersion = Path.GetFileName(sdkFolders.Single());
            result.StdOut.Trim().Should().Be(expectedSdkVersion);
        }
    }
}
