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

            var dotnetFolder = Path.GetDirectoryName(SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath);
            string sdkVersion = result.StdOut.Trim();
            Directory.Exists(Path.Combine(dotnetFolder, "sdk", sdkVersion))
                .Should().BeTrue($"dotnet --version should return an SDK installed under {dotnetFolder}");
        }
    }
}
