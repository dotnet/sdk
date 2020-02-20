﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using System.IO;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using System.Reflection;
using dotnet.Tests;
using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Utilities;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnettestBuildsAndRunsTestfromCsproj : SdkTest
    {
        public GivenDotnettestBuildsAndRunsTestfromCsproj(ITestOutputHelper log) : base(log)
        {
        }

        private readonly string [] ConsoleLoggerOutputNormal = new[] { "--logger", "console;verbosity=normal" };

        [Fact]
        public void MSTestSingleTFM()
        {
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp("3");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(ConsoleLoggerOutputNormal);

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 1");
                result.StdOut.Should().Contain("\u221a VSTestPassTest");
                result.StdOut.Should().Contain("X VSTestFailTest");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void ItImplicitlyRestoresAProjectWhenTesting()
        {
            string testAppName = "VSTestCore";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource()
                            .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(ConsoleLoggerOutputNormal);

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 1");
                result.StdOut.Should().Contain("\u221a VSTestPassTest");
                result.StdOut.Should().Contain("X VSTestFailTest");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void ItDoesNotImplicitlyRestoreAProjectWhenTestingWithTheNoRestoreOption()
        {
            string testAppName = "VSTestCore";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource()
                            .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            new DotnetTestCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute(ConsoleLoggerOutputNormal.Concat(new[] { "--no-restore", "/p:IsTestProject=true" }))
                .Should().Fail()
                .And.HaveStdOutContaining("project.assets.json");
        }

        [Fact]
        public void ItDoesNotRunTestsIfThereIsNoIsTestProject()
        {
            string testAppName = "VSTestCore";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource()
                            .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            new DotnetTestCommand(Log, ConsoleLoggerOutputNormal)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--no-restore", "/p:IsTestProject=''")
                .Should().Pass();
        }

        [Fact]
        public void XunitSingleTFM()
        {
            // Copy XunitCore project in output directory of project dotnet-vstest.Tests
            string testAppName = "XunitCore";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, identifier: "4")
                            .WithSource()
                            .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            // Restore project XunitCore
            new RestoreCommand(Log, testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(ConsoleLoggerOutputNormal);

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 1");
                result.StdOut.Should().Contain("\u221a TestNamespace.VSTestXunitTests.VSTestXunitPassTest");
                result.StdOut.Should().Contain("X TestNamespace.VSTestXunitTests.VSTestXunitFailTest");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void GivenAFailingTestItDisplaysFailureDetails()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("XunitCore")
                .WithSource()
                .WithVersionVariables();

            var result = new DotnetTestCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            result.ExitCode.Should().Be(1);

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("X TestNamespace.VSTestXunitTests.VSTestXunitFailTest");
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 1");
            }
        }

        [Fact]
        public void ItAcceptsMultipleLoggersAsCliArguments()
        {
            // Copy and restore VSTestCore project in output directory of project dotnet-vstest.Tests
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp("10");
            var trxFileNamePattern = "custom*.trx";
            string trxLoggerDirectory = Path.Combine(testProjectDirectory, "RD");

            // Delete trxLoggerDirectory if it exist
            if (Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }

            // Call test with logger enable
            CommandResult result = new DotnetTestCommand(Log)
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .Execute("--logger", "trx;logfilename=custom.trx", "--logger",
                                            "console;verbosity=normal", "--", "RunConfiguration.ResultsDirectory=" + trxLoggerDirectory);

            // Verify
            if (!TestContext.IsLocalized())
            {
                // We append current date time to trx file name, hence modifying this check
                Assert.True(Directory.EnumerateFiles(trxLoggerDirectory, trxFileNamePattern).Any());

                result.StdOut.Should().Contain("\u221a VSTestPassTest");
                result.StdOut.Should().Contain("X VSTestFailTest");
            }

            // Cleanup trxLoggerDirectory if it exist
            if (Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }
        }

        [Fact]
        public void TestWillNotBuildTheProjectIfNoBuildArgsIsGiven()
        {
            // Copy and restore VSTestCore project in output directory of project dotnet-vstest.Tests
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp("5");
            string configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            string expectedError = Path.Combine(testProjectDirectory, "bin",
                                   configuration, "netcoreapp3.0", "VSTestCore.dll");
            expectedError = "The test source file " + "\"" + expectedError + "\"" + " provided was not found.";

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .Execute("--no-build", "-v:m");

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().NotContain("Restore");
                //  https://github.com/dotnet/sdk/issues/3684
                //  Disable expected error check, it is sometimes giving the following error:
                //  The argument /opt/code/artifacts-ubuntu.18.04/tmp/Debug/bin/5/VSTestCore/bin/Debug/netcoreapp3.0/VSTestCore.dll is invalid. Please use the /help option to check the list of valid arguments
                //result.StdErr.Should().Contain(expectedError);
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void TestWillCreateTrxLoggerInTheSpecifiedResultsDirectoryBySwitch()
        {
            // Copy and restore VSTestCore project in output directory of project dotnet-vstest.Tests
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp("6");

            string trxLoggerDirectory = Path.Combine(testProjectDirectory, "TR", "x.y");

            // Delete trxLoggerDirectory if it exist
            if (Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }

            // Call test with trx logger enabled and results directory explicitly specified.
            CommandResult result = new DotnetTestCommand(Log)
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .Execute("--logger", "trx", "-r", trxLoggerDirectory);

            // Verify
            String[] trxFiles = Directory.GetFiles(trxLoggerDirectory, "*.trx");
            Assert.Single(trxFiles);
            result.StdOut.Should().Contain(trxFiles[0]);

            // Cleanup trxLoggerDirectory if it exist
            if(Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }
        }

        [Fact]
        public void ItCreatesTrxReportInTheSpecifiedResultsDirectoryByArgs()
        {
            // Copy and restore VSTestCore project in output directory of project dotnet-vstest.Tests
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp("7");
            var trxFileNamePattern = "custom*.trx";
            string trxLoggerDirectory = Path.Combine(testProjectDirectory, "RD");

            // Delete trxLoggerDirectory if it exist
            if (Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }

            // Call test with logger enable
            CommandResult result = new DotnetTestCommand(Log)
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .Execute("--logger", "trx;logfilename=custom.trx", "--",
                                                "RunConfiguration.ResultsDirectory=" + trxLoggerDirectory);

            // Verify
            // We append current date time to trx file name, hence modifying this check
            Assert.True(Directory.EnumerateFiles(trxLoggerDirectory, trxFileNamePattern).Any());

            // Cleanup trxLoggerDirectory if it exist
            if (Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }
        }

        [Fact]
        public void ItBuildsAndTestsAppWhenRestoringToSpecificDirectory()
        {
            // Creating folder with name short name "RestoreTest" to avoid PathTooLongException
            var rootPath = _testAssetsManager.CopyTestAsset("VSTestCore", identifier: "8")
                .WithSource()
                .WithVersionVariables()
                .Path;

            
            string pkgDir;
            //if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            //{
            //    // Moving pkgs folder on top to avoid PathTooLongException
            //    pkgDir = Path.Combine(RepoDirectoriesProvider.TestWorkingFolder, "pkgs");
            //}
            //else
            {
                pkgDir = Path.Combine(rootPath, "pkgs");
            }

            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("--packages", pkgDir)
                .Should()
                .Pass();

            new DotnetBuildCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("--no-restore")
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            CommandResult result = new DotnetTestCommand(Log, ConsoleLoggerOutputNormal)
                                        .WithWorkingDirectory(rootPath)
                                        .Execute("--no-restore");

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 1");
                result.StdOut.Should().Contain("\u221a VSTestPassTest");
                result.StdOut.Should().Contain("X VSTestFailTest");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void ItUsesVerbosityPassedToDefineVerbosityOfConsoleLoggerOfTheTests()
        {
            // Copy and restore VSTestCore project in output directory of project dotnet-vstest.Tests
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp("9");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute("-v", "q");

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 1");
                result.StdOut.Should().NotContain("\u221a TestNamespace.VSTestTests.VSTestPassTest");
                result.StdOut.Should().NotContain("X TestNamespace.VSTestTests.VSTestFailTest");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void ItTestsWithTheSpecifiedRuntimeOption()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("XunitCore")
                            .WithSource()
                            .WithVersionVariables();

            var rootPath = testInstance.Path;
            var rid = EnvironmentInfo.GetCompatibleRid();

            new DotnetBuildCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("--runtime", rid)
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            var result = new DotnetTestCommand(Log, ConsoleLoggerOutputNormal)
                .WithWorkingDirectory(rootPath)
                .Execute("--no-build", "--runtime", rid);

            result
                .Should()
                .NotHaveStdErrContaining("MSB1001")
                .And
                .HaveStdOutContaining(rid);

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 1");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void ItAcceptsNoLogoAsCliArguments()
        {
            // Copy and restore VSTestCore project in output directory of project dotnet-vstest.Tests
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp("14");

            // Call test with logger enable
            CommandResult result = new DotnetTestCommand(Log)
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .Execute("--nologo");

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().NotContain("Microsoft (R) Test Execution Command Line Tool Version");
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 1");
            }
        }

        [WindowsOnlyFact]
        public void ItCreatesCoverageFileWhenCodeCoverageEnabledByRunsettings()
        {
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp("11");

            string resultsDirectory = Path.Combine(testProjectDirectory, "RD");

            // Delete resultsDirectory if it exist
            if (Directory.Exists(resultsDirectory))
            {
                Directory.Delete(resultsDirectory, true);
            }

            var settingsPath =Path.Combine(AppContext.BaseDirectory, "CollectCodeCoverage.runsettings");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(
                                            "--settings", settingsPath,
                                            "--results-directory", resultsDirectory);

            File.WriteAllText(Path.Combine(testProjectDirectory, "output.txt"),
                                result.StdOut + Environment.NewLine + result.StdErr);

            // Verify test results
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 1");
            }

            // Verify coverage file.
            DirectoryInfo d = new DirectoryInfo(resultsDirectory);
            FileInfo[] coverageFileInfos = d.GetFiles("*.coverage", SearchOption.AllDirectories);
            Assert.Single(coverageFileInfos);

            result.ExitCode.Should().Be(1);
        }

        [WindowsOnlyFact]
        public void ItCreatesCoverageFileInResultsDirectory()
        {
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp("12");

            string resultsDirectory = Path.Combine(testProjectDirectory, "RD");

            // Delete resultsDirectory if it exist
            if (Directory.Exists(resultsDirectory))
            {
                Directory.Delete(resultsDirectory, true);
            }

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(
                                            "--collect", "Code Coverage",
                                            "--results-directory", resultsDirectory);

            // Verify test results
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 1");
            }

            // Verify coverage file.
            DirectoryInfo d = new DirectoryInfo(resultsDirectory);
            FileInfo[] coverageFileInfos = d.GetFiles("*.coverage", SearchOption.AllDirectories);
            Assert.Single(coverageFileInfos);

            result.ExitCode.Should().Be(1);
        }

        [UnixOnlyFact]
        public void ItShouldShowWarningMessageOnCollectCodeCoverage()
        {
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp("13");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(
                                            "--collect", "Code Coverage",
                                            "--filter", "VSTestPassTest");

            // Verify test results
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("No code coverage data available. Code coverage is currently supported only on Windows.");
                result.StdOut.Should().Contain("Total tests: 1");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Test Run Successful.");
            }

            result.ExitCode.Should().Be(0);
        }

        [Fact]
        public void ItShouldNotShowImportantMessage()
        {
            string testAppName = "VSTestCore";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .WithVersionVariables()
                .WithProjectChanges(ProjectModification.AddDisplayMessageBeforeVsTestToProject);

            var testProjectDirectory = testInstance.Path;

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute();

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().NotContain("Important text");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void ItShouldShowImportantMessageWhenInteractiveFlagIsPassed()
        {
            string testAppName = "VSTestCore";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .WithVersionVariables()
                .WithProjectChanges(ProjectModification.AddDisplayMessageBeforeVsTestToProject);

            var testProjectDirectory = testInstance.Path;

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--interactive");

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Important text");
            }

            result.ExitCode.Should().Be(1);
        }

        private string CopyAndRestoreVSTestDotNetCoreTestApp([CallerMemberName] string callingMethod = "")
        {
            // Copy VSTestCore project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestCore";

            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, callingMethod: callingMethod)
                            .WithSource()
                            .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            // Restore project VSTestCore
            new RestoreCommand(Log, testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            return testProjectDirectory;
        }
    }
}
