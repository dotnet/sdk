// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Test.Utilities;
using DotnetCommand = Microsoft.DotNet.Tools.Test.Utilities.DotnetCommand;
using AssemblyInfo = Microsoft.DotNet.Tools.Test.Utilities.AssemblyInfo;

namespace EndToEnd.Tests
{
    public class TelemetryOptOutDefault : TestBase
    {
        [Fact]
        public void TelemetryOptOutDefaultAttribute()
        {
            var versionCommand = new DotnetCommand().ExecuteWithCapturedOutput("--version");
            var sdkVersion = versionCommand.StdOut.Trim();
            var dotnetdir = Path.Combine(Path.GetDirectoryName(RepoDirectoriesProvider.DotnetUnderTest), "sdk", sdkVersion);
            var result = AssemblyInfo.Get(Path.Combine(dotnetdir, "dotnet.dll"), "AssemblyMetadataAttribute");
            result.Should().Contain("TelemetryOptOutDefault:False");
        }
    }
}
