using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace EndToEnd.Tests
{
    public class TelemetryOptOutDefault : TestBase
    {
        [Fact]
        public void TelemetryOptOutDefaultAttribute()
        {
            var result = new DotnetCommand()
                .ExecuteWithCapturedOutput("--version");

            var sdkVersion = result.StdOut.Trim();

            var dotnetdir = Path.Combine(Path.GetDirectoryName(RepoDirectoriesProvider.DotnetUnderTest), "sdk", sdkVersion);
            var result = AssemblyInfo.Get(Path.Combine(dotnetdir, "dotnet.dll"), "AssemblyMetadataAttribute");
            result.Should().Contain("TelemetryOptOutDefault:False");
        }
    }
}
