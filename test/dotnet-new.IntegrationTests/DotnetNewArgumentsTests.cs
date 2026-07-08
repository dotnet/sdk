// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [TestClass]
    public partial class DotnetNewArgumentsTests
    {
        public TestContext TestContext { get; set; } = null!;
        private ITestOutputHelper _log => new TestContextOutputHelper(TestContext);

        [TestMethod]
        public void ShowsDetailedOutputOnMissedRequiredParam()
        {
            var dotnetNewHelpOutput = new DotnetNewCommand(_log, "--help")
                .WithoutCustomHive()
                .Execute();

            new DotnetNewCommand(_log, "-v")
                .WithoutCustomHive()
                .Execute()
                .Should()
                .ExitWith(127)
                .And.HaveStdErrContaining("Required argument missing for option: '-v'")
                .And.HaveStdOutContaining(dotnetNewHelpOutput.StdOut ?? string.Empty);
        }
    }
}
