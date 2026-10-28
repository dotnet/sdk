// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [TestClass]
    public class FirstRunTest : BaseIntegrationTest
    {
        private ITestOutputHelper _log => Log;

        public FirstRunTest()
        {
        }

        [TestMethod]
        public void FirstRunSuccess()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log)
                .WithCustomHive(home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Error");

            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("classlib");
        }
    }
}
