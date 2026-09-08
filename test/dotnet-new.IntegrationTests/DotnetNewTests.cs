// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [TestClass]
    public class DotnetNewTests : BaseIntegrationTest
    {
        private ITestOutputHelper _log => Log;
        private static SharedHomeDirectory s_sharedHome = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext ctx)
        {
            s_sharedHome = new SharedHomeDirectory(new TestContextOutputHelper(ctx));
        }

        [ClassCleanup]
        public static void ClassCleanup() => s_sharedHome?.Dispose();

        private SharedHomeDirectory _sharedHome => s_sharedHome;

        [TestMethod]
        public Task CanShowBasicInfo()
        {
            CommandResult commandResult = new DotnetNewCommand(_log)
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0).And.NotHaveStdErr();

            return Verify(commandResult.StdOut).UniqueForOSPlatform();
        }

        [TestMethod]
        [DataRow("-v", "q")]
        [DataRow("-v", "quiet")]
        [DataRow("--verbosity", "q")]
        [DataRow("--verbosity", "quiet")]
        public void CanUseQuietMode(string optionName, string optionValue)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "search", "template-does-not-exist", optionName, optionValue)
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(103)
                .And.NotHaveStdErr()
                .And.NotHaveStdOut();
        }

        [TestMethod]
        public void CanUseQuietMode_ViaEnvVar()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "search", "template-does-not-exist")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_OUTPUT", "false")
                .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_ERROR", "false")
                .Execute();

            commandResult.Should()
                .ExitWith(103)
                .And.NotHaveStdErr()
                .And.NotHaveStdOut();
        }

        [TestMethod]
        [DataRow("-v", "m")]
        [DataRow("-v", "minimal")]
        [DataRow("--verbosity", "m")]
        [DataRow("--verbosity", "minimal")]
        public Task CanUseMinimalMode(string optionName, string optionValue)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "search", "template-does-not-exist", optionName, optionValue)
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                 .ExitWith(103)
                 .And.NotHaveStdOut();

            return Verify(commandResult.StdErr)
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

        [TestMethod]
        [DataRow("-v", "n")]
        [DataRow("-v", "normal")]
        [DataRow("--verbosity", "n")]
        [DataRow("--verbosity", "normal")]
        public Task CanUseNormalMode(string optionName, string optionValue)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "search", "template-does-not-exist", optionName, optionValue)
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                 .ExitWith(103);

            return Verify(commandResult.FormatOutputStreams())
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

        [TestMethod]
        [DataRow("-v", "diag")]
        [DataRow("-v", "diagnostic")]
        [DataRow("--verbosity", "diag")]
        [DataRow("--verbosity", "diagnostic")]
        [DataRow("--diagnostics", null)]
        [DataRow("-d", null)]
        public void CanUseDiagMode(string optionName, string? optionValue)
        {
            CommandResult commandResult = new DotnetNewCommand(
                _log,
                string.IsNullOrEmpty(optionValue)
                    ? new[] { "search", "template-does-not-exist", optionName }
                    : new[] { "search", "template-does-not-exist", optionName, optionValue })
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                 .ExitWith(103)
                 .And.HaveStdOutContaining("[Debug] [Template Engine] => [Execute]: Execute started");
        }

        [TestMethod]
        public void CanUseDebugPathWhenEnvVarIsSet_Instantiate()
        {
            string cliHomePath = CreateTemporaryFolder(folderName: "CLI_HOME_TEST_FOLDER");
            string home = CreateTemporaryFolder(folderName: "Home");

            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--dry-run")
                .WithDebug()
                .WithCustomHive(home)
                .WithEnvironmentVariable("DOTNET_CLI_HOME", cliHomePath)
                .Execute();

            commandResult
                .Should()
                .HaveStdOutContaining($"Settings Location: {home}")
                .And
                .NotHaveStdOutContaining($"Settings Location: {cliHomePath}")
                .And
                .Pass();
        }

        [TestMethod]
        public void CanUseEnvVarPathWhenDebugPathIsNotSet_Instantiate()
        {
            string cliHomePath = CreateTemporaryFolder(folderName: "CLI_HOME_TEST_FOLDER");

            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--dry-run")
                .WithDebug()
                .WithoutCustomHive()
                .WithEnvironmentVariable("DOTNET_CLI_HOME", cliHomePath)
                .Execute();

            commandResult
                .Should()
                .HaveStdOutContaining($"Settings Location: {cliHomePath}")
                .And
                .Pass();
        }
    }
}
