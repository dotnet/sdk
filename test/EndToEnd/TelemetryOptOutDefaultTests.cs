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
            var dotnetdir = Path.Combine(Path.GetDirectoryName(RepoDirectoriesProvider.DotnetUnderTest), "sdk", "7.0.100");
            var result = AssemblyInfo.Get(Path.Combine(dotnetdir, "dotnet.dll"), "AssemblyMetadataAttribute");
            result.Should().Contain("TelemetryOptOutDefault:False");
        }
    }
}
