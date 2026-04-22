// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenDotnetRunThrowsAParseError : SdkTest
    {
        public GivenDotnetRunThrowsAParseError(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItFailsWithAnAppropriateErrorMessage()
        {
            // Use a dedicated empty directory because file-based program support means that dotnet run will
            //  pick up stray .cs files from other tests.
            var emptyDir = TestAssetsManager.CreateTestDirectory().Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(emptyDir)
                .Execute("--", "1")
                .Should().Fail()
                .And.HaveStdErrContainingOnce("Couldn't find a project to run.");
        }
    }
}
