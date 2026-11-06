// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using FakeItEasy;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    [TestClass]
    public class TemplatePackageDisplayTest
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void DisplayUpdateCheckResultTest()
        {
            var fakeOutputReporter = new FakeReporter();
            var fakeErrorReporter = new FakeReporter();
            var packageDisplayer = new TemplatePackageDisplay(fakeOutputReporter, fakeErrorReporter);
            var vulnerabilities = new List<VulnerabilityInfo>
            {
                new VulnerabilityInfo(1, new List<string> { "moderate1", "moderate2" }),
                new VulnerabilityInfo(0, new List<string> { "low1" })
            };
            var updateCheckResult = CheckUpdateResult.CreateFailure(
                A.Fake<IManagedTemplatePackage>(),
                InstallerErrorCode.VulnerablePackage,
                "test message",
                vulnerabilities);
            var expectedErrors =
@"[31m[1mThe package  contains vulnerabilities:[22m[39m

    [1m[34mModerate[39m[22m:
        moderate1
        moderate2

    [1m[36mLow[39m[22m:
        low1".UnixifyLineBreaks();

            packageDisplayer.DisplayUpdateCheckResult(updateCheckResult, A.Fake<ICommandArgs>());

            var reportedErrors = fakeErrorReporter.ReportedStrings.ToString().UnixifyLineBreaks().Trim();
            fakeOutputReporter.ReportedStrings.ToString().Should().BeNullOrEmpty();
            reportedErrors.Should().NotBeEmpty();
            Assert.AreEqual(expectedErrors, reportedErrors);
        }

        [TestMethod]
        public async Task DisplayInstallResultTest_WithForceSpecified()
        {
            var fakeOutputReporter = new FakeReporter();
            var fakeErrorReporter = new FakeReporter();
            var packageDisplayer = new TemplatePackageDisplay(fakeOutputReporter, fakeErrorReporter);
            var vulnerabilities = new List<VulnerabilityInfo>
            {
                new VulnerabilityInfo(3, new List<string> { "critical1", "critical2", "critical3" }),
                new VulnerabilityInfo(0, new List<string> { "low1" })
            };
            var managedTPA = GetFakedManagedTemplatePackage("testMountA", "PackageA");

            var installResult = InstallResult.CreateSuccess(
                new InstallRequest("PackageA"),
                managedTPA,
                vulnerabilities);
            var expectedOutput =
@"No templates were found in the package PackageA.
Installed package has the following vulnerabilities:

    [1m[31mCritical[39m[22m:
        critical1
        critical2
        critical3

    [1m[36mLow[39m[22m:
        low1".UnixifyLineBreaks();
            ParseResult parseResult = CliTestHostFactory.CreateNewCommand(CliTestHostFactory.GetVirtualHost()).Parse($"new search foo");

            await packageDisplayer.DisplayInstallResultAsync(
                "PackageA",
                installResult,
                parseResult,
                force: true,
                A.Fake<TemplatePackageManager>(),
                A.Fake<IEngineEnvironmentSettings>(),
                A.Fake<TemplateConstraintManager>(),
                TestContext.CancellationToken);

            var reportedOutput = fakeOutputReporter.ReportedStrings.ToString().UnixifyLineBreaks().Trim();
            reportedOutput.Should().NotBeEmpty();
            fakeErrorReporter.ReportedStrings.ToString().Should().BeEmpty();
            Assert.AreEqual(expectedOutput, reportedOutput);
        }

        [TestMethod]
        public async Task DisplayInstallResultTest()
        {
            var fakeOutputReporter = new FakeReporter();
            var fakeErrorReporter = new FakeReporter();
            var packageDisplayer = new TemplatePackageDisplay(fakeOutputReporter, fakeErrorReporter);
            var vulnerabilities = new List<VulnerabilityInfo>
            {
                new VulnerabilityInfo(1, new List<string> { "moderate1", "moderate2" }),
                new VulnerabilityInfo(0, new List<string> { "low1" })
            };
            var installResult = InstallResult.CreateFailure(
                new InstallRequest("testPackage"),
                InstallerErrorCode.VulnerablePackage,
                "test message",
                vulnerabilities);
            var expectedErrors =
@"[31m[1mFailed to install testPackage, due to detected vulnerabilities:[22m[39m

    [1m[34mModerate[39m[22m:
        moderate1
        moderate2

    [1m[36mLow[39m[22m:
        low1

[1mIn order to install this package, run:[22m
   new install testPackage --force".UnixifyLineBreaks();

            ParseResult parseResult = CliTestHostFactory.CreateNewCommand(CliTestHostFactory.GetVirtualHost()).Parse($"new search foo");
            await packageDisplayer.DisplayInstallResultAsync(
                "testPackage",
                installResult,
                parseResult,
                force: false,
                A.Fake<TemplatePackageManager>(),
                A.Fake<IEngineEnvironmentSettings>(),
                A.Fake<TemplateConstraintManager>(),
                TestContext.CancellationToken);

            var reportedErrors = fakeErrorReporter.ReportedStrings.ToString().UnixifyLineBreaks().Trim();
            fakeOutputReporter.ReportedStrings.ToString().Should().BeNullOrEmpty();
            reportedErrors.Should().NotBeEmpty();
            Assert.AreEqual(expectedErrors, reportedErrors);
        }

        [TestMethod]
        public async Task DisplayInstallResultTest_UpdateRequest()
        {
            var fakeOutputReporter = new FakeReporter();
            var fakeErrorReporter = new FakeReporter();
            var packageDisplayer = new TemplatePackageDisplay(fakeOutputReporter, fakeErrorReporter);
            var vulnerabilities = new List<VulnerabilityInfo>
            {
                new VulnerabilityInfo(2, new List<string> { "high" })
            };
            var managedTPA = GetFakedManagedTemplatePackage("testMountA", "PackageA");
            var updateResult = UpdateResult.CreateFailure(
                new UpdateRequest(managedTPA, "1.0.0"),
                InstallerErrorCode.VulnerablePackage,
                "test message",
                vulnerabilities);
            var expectedErrors =
@"[31m[1mThe package testPackage was not updated due to detected vulnerabilities:[22m[39m

    [1m[33mHigh[39m[22m:
        high

[1mIn order to update this package, run:[22m
   new uninstall testPackage
   new install testPackage --force".UnixifyLineBreaks();

            ParseResult parseResult = CliTestHostFactory.CreateNewCommand(CliTestHostFactory.GetVirtualHost()).Parse($"new search foo");
            await packageDisplayer.DisplayInstallResultAsync(
                "testPackage",
                updateResult,
                parseResult,
                force: false,
                A.Fake<TemplatePackageManager>(),
                A.Fake<IEngineEnvironmentSettings>(),
                A.Fake<TemplateConstraintManager>(),
                TestContext.CancellationToken);

            var reportedErrors = fakeErrorReporter.ReportedStrings.ToString().UnixifyLineBreaks().Trim();
            fakeOutputReporter.ReportedStrings.ToString().Should().BeNullOrEmpty();
            reportedErrors.Should().NotBeEmpty();
            Assert.AreEqual(expectedErrors, reportedErrors);
        }

        [TestMethod]
        [CombinatorialData]
        public async Task DisplayInstallResult_PackageNotFound_PreservesDiagnostic(
            bool update,
            [CombinatorialValues(
                "Package source mapping is enabled, but no source was found for package testPackage.",
                "testPackage was not found in NuGet feeds testFeed.")] string errorMessage)
        {
            var outputReporter = new FakeReporter();
            var errorReporter = new FakeReporter();
            var packageDisplayer = new TemplatePackageDisplay(outputReporter, errorReporter);
            InstallerOperationResult result = update
                ? UpdateResult.CreateFailure(
                    new UpdateRequest(GetFakedManagedTemplatePackage("testMount", "testPackage"), "1.0.0"),
                    InstallerErrorCode.PackageNotFound,
                    errorMessage,
                    [])
                : InstallResult.CreateFailure(
                    new InstallRequest("testPackage"),
                    InstallerErrorCode.PackageNotFound,
                    errorMessage,
                    []);
            ParseResult parseResult = CliTestHostFactory.CreateNewCommand(CliTestHostFactory.GetVirtualHost()).Parse("new install testPackage");

            await packageDisplayer.DisplayInstallResultAsync(
                "testPackage",
                result,
                parseResult,
                force: false,
                A.Fake<TemplatePackageManager>(),
                A.Fake<IEngineEnvironmentSettings>(),
                A.Fake<TemplateConstraintManager>(),
                TestContext.CancellationToken);

            Assert.AreEqual(
                $"testPackage could not be installed.{Environment.NewLine}{errorMessage}".Bold().Red(),
                errorReporter.ReportedStrings.ToString().Trim());
            Assert.AreEqual(string.Empty, outputReporter.ReportedStrings.ToString());
        }

        [TestMethod]
        [CombinatorialData]
        public async Task DisplayInstallResult_PackageNotFound_WithoutDiagnostic(
            bool update,
            [CombinatorialValues(null, "", " ")] string? errorMessage)
        {
            var outputReporter = new FakeReporter();
            var errorReporter = new FakeReporter();
            var packageDisplayer = new TemplatePackageDisplay(outputReporter, errorReporter);
            InstallerOperationResult result = update
                ? UpdateResult.CreateFailure(
                    new UpdateRequest(GetFakedManagedTemplatePackage("testMount", "testPackage"), "1.0.0"),
                    InstallerErrorCode.PackageNotFound,
                    errorMessage!,
                    [])
                : InstallResult.CreateFailure(
                    new InstallRequest("testPackage"),
                    InstallerErrorCode.PackageNotFound,
                    errorMessage!,
                    []);
            ParseResult parseResult = CliTestHostFactory.CreateNewCommand(CliTestHostFactory.GetVirtualHost()).Parse("new install testPackage");

            await packageDisplayer.DisplayInstallResultAsync(
                "testPackage",
                result,
                parseResult,
                force: false,
                A.Fake<TemplatePackageManager>(),
                A.Fake<IEngineEnvironmentSettings>(),
                A.Fake<TemplateConstraintManager>(),
                TestContext.CancellationToken);

            Assert.AreEqual(
                "testPackage could not be installed, the package does not exist.".Bold().Red(),
                errorReporter.ReportedStrings.ToString().Trim());
            Assert.AreEqual(string.Empty, outputReporter.ReportedStrings.ToString());
        }

        [TestMethod]
        [CombinatorialData]
        public void DisplayUpdateCheckResult_PackageNotFound_PreservesDiagnostic(
            bool explicitCheck,
            bool localPackage,
            [CombinatorialValues(null, "", " ", "Package source mapping is enabled, but no source was found for package testPackage.")] string? errorMessage)
        {
            var outputReporter = new FakeReporter();
            var errorReporter = new FakeReporter();
            var packageDisplayer = new TemplatePackageDisplay(outputReporter, errorReporter);
            var package = GetFakedManagedTemplatePackage("testMount", "testPackage");
            A.CallTo(() => package.IsLocalPackage).Returns(localPackage);
            var result = CheckUpdateResult.CreateFailure(package, InstallerErrorCode.PackageNotFound, errorMessage!, []);
            ParseResult parseResult = CliTestHostFactory.CreateNewCommand(CliTestHostFactory.GetVirtualHost()).Parse("new update --check-only");
            var args = new GlobalArgs(parseResult);

            if (explicitCheck)
            {
                packageDisplayer.DisplayUpdateCheckResults(A.Fake<IEngineEnvironmentSettings>(), [result], args, showUpdates: false);
            }
            else
            {
                packageDisplayer.DisplayUpdateCheckResult(result, args);
            }

            string expectedError = string.Empty;
            if (explicitCheck || !localPackage)
            {
                expectedError = "Failed to check update for testPackage: the package is not available in configured NuGet feeds.".Bold().Red();
                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    expectedError += Environment.NewLine + errorMessage.Bold().Red();
                }
            }
            Assert.AreEqual(expectedError, errorReporter.ReportedStrings.ToString().Trim());
            Assert.AreEqual(string.Empty, outputReporter.ReportedStrings.ToString());
        }

        private IManagedTemplatePackage GetFakedManagedTemplatePackage(string mountPointUri, string displayName)
        {
            var managedTemplatePackage = A.Fake<IManagedTemplatePackage>();
            A.CallTo(() => managedTemplatePackage.MountPointUri).Returns(mountPointUri);
            A.CallTo(() => managedTemplatePackage.DisplayName).Returns(displayName);

            return managedTemplatePackage;
        }

        private class FakeReporter : IReporter
        {
            public StringBuilder ReportedStrings { get; set; } = new StringBuilder();

            public void Write(string message) => ReportedStrings.Append(message);

            public void WriteLine(string message) => ReportedStrings.AppendLine(message);

            public void WriteLine() => ReportedStrings.AppendLineN();

            public void WriteLine(string format, params object?[] args) => WriteLine(string.Format(format, args));
        }
    }
}
