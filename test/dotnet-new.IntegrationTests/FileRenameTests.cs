// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [TestClass]
    public class FileRenameTests : BaseIntegrationTest
    {
        private ITestOutputHelper _log => Log;

        public FileRenameTests()
        {
        }

        [TestMethod]
        public void CanUseFileRenameWithNowGenerator()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithFileRenameDate", _log, home, workingDirectory);
            new DotnetNewCommand(_log, "TestAssets.TemplateWithFileRenameDate", "--migrationName", "MyTestName")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"TestAssets.TemplateWithFileRenameDate\" was created successfully.");

            DirectoryInfo directoryInfo = new(workingDirectory);
            Assert.MatchesRegex("\\d{8}_mytestname.cs", directoryInfo.EnumerateFiles().Single().Name);
        }
    }
}
