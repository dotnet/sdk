// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Test.Utilities;
using DotnetCommand = Microsoft.DotNet.Tools.Test.Utilities.DotnetCommand;

namespace EndToEnd.Tests
{
    public class VersionTests : TestBase
    {
        [Fact]
        public void DotnetVersionReturnsCorrectVersion()
        {
            var result = new DotnetCommand().ExecuteWithCapturedOutput("--version");
            result.Should().Pass();

            var dotnetFolder = Path.GetDirectoryName(RepoDirectoriesProvider.DotnetUnderTest);
            var sdkFolders = Directory.GetDirectories(Path.Combine(dotnetFolder, "sdk"));
            sdkFolders.Length.Should().Be(1, "Only one SDK folder is expected in the layout");

            var expectedSdkVersion = Path.GetFileName(sdkFolders.Single());
            result.StdOut.Trim().Should().Be(expectedSdkVersion);
        }
    }
}
