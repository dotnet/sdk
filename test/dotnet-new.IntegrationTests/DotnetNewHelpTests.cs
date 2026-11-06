// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewHelpTests
    {
        private ITestOutputHelper _log => Log;
        private static SharedHomeDirectory s_fixture = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext ctx)
        {
            s_fixture = new SharedHomeDirectory(new TestContextOutputHelper(ctx));
        }

        [ClassCleanup]
        public static void ClassCleanup() => s_fixture?.Dispose();

        private SharedHomeDirectory _fixture => s_fixture;

        [TestMethod]
        public void WontShowLanguageHintInCaseOfOneLang()
        {
            string workingDirectory = CreateTemporaryFolder();

            new DotnetNewCommand(_log, "globaljson", "--help")
                    .WithCustomHive(_fixture.HomeDirectory)
                    .WithWorkingDirectory(workingDirectory)
                    .Execute()
                    .Should().Pass()
                    .And.NotHaveStdErr()
                    .And.HaveStdOutContaining("global.json file")
                    .And.NotHaveStdOutContaining("To see help for other template languages");
        }
    }
}
