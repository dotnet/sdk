// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tests
{
    [TestClass]
    public class GivenParserDirectives : SdkTest
    {
        public GivenParserDirectives()
        {
        }

        [TestMethod]
        public void ItCanAcceptResponseFiles()
        {
            var testDirectory = TestAssetsManager.CreateTestDirectory().Path;
            File.WriteAllText(Path.Combine(testDirectory, "response.rsp"), "build");
            string[] args = new[] { @"@response.rsp", "-h" };
            new DotnetCommand(Log, args)
                .WithWorkingDirectory(testDirectory)
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(@"dotnet build [<PROJECT | SOLUTION | FILE>...] [options]");
        }
    }
}
