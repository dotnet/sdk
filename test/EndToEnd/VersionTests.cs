using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace EndToEnd.Tests
{
    public class VersionTests : TestBase
    {
        [Fact]
        public void DotnetVersionReturnsCorrectVersion()
        {
            var result = new DotnetCommand()
                .ExecuteWithCapturedOutput("--version");

            result.Should().Pass();

            var dotnetFolder = Path.GetDirectoryName(RepoDirectoriesProvider.DotnetUnderTest);

            var sdkFolders = Directory.GetDirectories(Path.Combine(dotnetFolder, "sdk"));

            sdkFolders.Length.Should().Be(1, "Only one SDK folder is expected in the layout");

            var expectedSdkVersion = Path.GetFileName(sdkFolders.Single());

            result.StdOut.Trim().Should().Be(expectedSdkVersion);
        }
    }
}
