// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [TestClass]
    public class DotnetNewDebugOptionsTests : BaseIntegrationTest
    {
        private ITestOutputHelper _log => Log;

        public DotnetNewDebugOptionsTests()
        {
        }

        [TestMethod]
        public void CanShowBasicInfoWithDebugReinit()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string cacheFilePath = Path.Combine(home, "dotnetcli", Product.Version, "templatecache.json");

            CommandResult commandResult = new DotnetNewCommand(_log)
                .WithCustomHive(home)
                .Execute();

            commandResult.Should().ExitWith(0).And.NotHaveStdErr();
            Assert.IsTrue(File.Exists(cacheFilePath));
            DateTime lastUpdateDate = File.GetLastWriteTimeUtc(cacheFilePath);

            CommandResult reinitCommandResult = new DotnetNewCommand(_log, "--debug:reinit")
               .WithCustomHive(home)
               .Execute();

            reinitCommandResult.Should().ExitWith(0).And.NotHaveStdErr();
            Assert.AreEqual(commandResult.StdOut, reinitCommandResult.StdOut);
            Assert.IsTrue(File.Exists(cacheFilePath));
            Assert.IsLessThan(lastUpdateDate, File.GetLastWriteTimeUtc(cacheFilePath));
        }

        [TestMethod]
        public void CanShowBasicInfoWithDebugRebuildCache()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string cacheFilePath = Path.Combine(home, "dotnetcli", Product.Version, "templatecache.json");

            CommandResult commandResult = new DotnetNewCommand(_log)
                .WithCustomHive(home)
                .Execute();

            commandResult.Should().ExitWith(0).And.NotHaveStdErr();
            Assert.IsTrue(File.Exists(cacheFilePath));
            DateTime lastUpdateDate = File.GetLastWriteTimeUtc(cacheFilePath);

            CommandResult reinitCommandResult = new DotnetNewCommand(_log, "--debug:rebuildcache")
               .WithCustomHive(home)
               .Execute();

            reinitCommandResult.Should().ExitWith(0).And.NotHaveStdErr();
            Assert.AreEqual(commandResult.StdOut, reinitCommandResult.StdOut);
            Assert.IsTrue(File.Exists(cacheFilePath));
            Assert.IsLessThan(lastUpdateDate, File.GetLastWriteTimeUtc(cacheFilePath));
        }

        [TestMethod]
        public Task CanShowConfigWithDebugShowConfig()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            CommandResult commandResult = new DotnetNewCommand(_log, "--debug:show-config")
               .WithCustomHive(home)
               .Execute();

            commandResult.Should().ExitWith(0).And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .UniqueForOSPlatform()
                .AddScrubber(output =>
                {
                    string finalOutput = output.ToString();
                    //remove versions
                    output.ScrubByRegex("Version=[A-Za-z0-9\\.]+", "Version=<version>");
                    //remove tokens
                    output.ScrubByRegex("PublicKeyToken=[A-Za-z0-9]+", "PublicKeyToken=<token>");

                    //removes the delimiter line as we don't know the length of last columns containing paths above
                    output.ScrubTableHeaderDelimiter();
                    //removes the spaces after "Assembly" column header as we don't know the amount of spaces after it
                    output.ScrubByRegex("Assembly *", "Assembly");
                });
        }

        [TestMethod]
        public void DoesNotCreateCacheWhenVirtualHiveIsUsed()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string envVariable = CliFolderPathCalculator.DotnetHomePath;

            new DotnetNewCommand(_log, "--debug:ephemeral-hive")
               .WithoutCustomHive()
               .WithEnvironmentVariable(envVariable, home)
               .Execute()
               .Should().Pass().And.NotHaveStdErr();

            Assert.IsEmpty(new DirectoryInfo(home).EnumerateFiles());
        }

        [TestMethod]
        public void DoesCreateCacheInDifferentLocationWhenCustomHiveIsUsed()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "--debug:custom-hive", home)
               .WithoutCustomHive()
               .Execute()
               .Should().Pass().And.NotHaveStdErr();

            string[] createdCacheEntries = Directory.GetFileSystemEntries(home);

            Assert.HasCount(2, createdCacheEntries);
            Assert.Contains(Path.Combine(home, "packages"), createdCacheEntries);
            Assert.IsTrue(File.Exists(Path.Combine(home, "dotnetcli", Product.Version, "templatecache.json")));
        }

        [TestMethod]
        public void CanDisableBuiltInTemplates_List()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "list", "--debug:disable-sdk-templates")
                .WithCustomHive(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("console")
                .And.HaveStdOutContaining("No templates installed.");
        }

        [TestMethod]
        public void CanDisableBuiltInTemplates_Instantiate()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--debug:disable-sdk-templates")
                .WithCustomHive(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.HaveStdErrContaining("No templates or subcommands found matching: 'console'.");
        }
    }
}
