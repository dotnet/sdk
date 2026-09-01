// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [TestClass]
    public class SourcesTests : BaseIntegrationTest
    {
        private ITestOutputHelper _log => Log;

        public SourcesTests()
        {
        }

        [TestMethod]
        public void EnsureItsPossibleToIncludePackagesLockJson()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("SourceWithExcludeAndWithout", _log, home, workingDirectory);
            new DotnetNewCommand(_log, "withexclude")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0);
            Assert.AreSequenceEqual(
                new[] { "packages.lock.json", "foo.cs", "bar.cs" }.OrderBy(s => s),
                Directory.EnumerateFiles(workingDirectory, "*", SearchOption.AllDirectories).Select(Path.GetFileName).OrderBy(s => s));

            workingDirectory = CreateTemporaryFolder();
            new DotnetNewCommand(_log, "withoutexclude")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0);
            Assert.AreSequenceEqual(
                new[] { "foo.cs", "bar.cs" }.OrderBy(s => s),
                Directory.EnumerateFiles(workingDirectory, "*", SearchOption.AllDirectories).Select(Path.GetFileName).OrderBy(s => s));
        }
    }
}
