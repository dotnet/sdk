// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Verifies test-only artifact discovery. Assumes dotnetup has been built in the selected artifacts directory.
/// </summary>
[TestClass]
public class DotnetupTestUtilitiesTests
{
    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void GetDotnetupExecutablePath_WithoutEnvironmentOverride_UsesTestAssemblyMetadata()
    {
        var originalArtifactsDir = Environment.GetEnvironmentVariable("ArtifactsDir");
        var originalExecutable = Environment.GetEnvironmentVariable("DOTNETUP_TEST_EXECUTABLE");
        try
        {
            Environment.SetEnvironmentVariable("ArtifactsDir", null);
            Environment.SetEnvironmentVariable("DOTNETUP_TEST_EXECUTABLE", null);
            var artifactsDir = typeof(DotnetupTestUtilitiesTests).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "ArtifactsDir").Value;
            if (string.IsNullOrWhiteSpace(artifactsDir))
            {
                throw new AssertFailedException("The test assembly must record its ArtifactsDir build property.");
            }

            var executable = DotnetupTestUtilities.GetDotnetupExecutablePath();

            executable.Should().StartWith(Path.GetFullPath(artifactsDir));
            File.Exists(executable).Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ArtifactsDir", originalArtifactsDir);
            Environment.SetEnvironmentVariable("DOTNETUP_TEST_EXECUTABLE", originalExecutable);
        }
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void GetDotnetupExecutablePath_MissingExplicitArtifactsDirectory_DoesNotUseOtherBuilds()
    {
        using var environment = new TestEnvironment();
        var originalArtifactsDir = Environment.GetEnvironmentVariable("ArtifactsDir");
        var originalExecutable = Environment.GetEnvironmentVariable("DOTNETUP_TEST_EXECUTABLE");
        try
        {
            Environment.SetEnvironmentVariable("ArtifactsDir", environment.TempRoot);
            Environment.SetEnvironmentVariable("DOTNETUP_TEST_EXECUTABLE", null);

            Action locate = () => DotnetupTestUtilities.GetDotnetupExecutablePath();

            locate.Should().Throw<FileNotFoundException>().Which.Message.Should().Contain(environment.TempRoot);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ArtifactsDir", originalArtifactsDir);
            Environment.SetEnvironmentVariable("DOTNETUP_TEST_EXECUTABLE", originalExecutable);
        }
    }

    [TestMethod]
    public void ArtifactsDirMetadata_IsNotEmbeddedInProductAssembly()
    {
        typeof(Parser).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Should().NotContain(attribute => attribute.Key == "ArtifactsDir");
    }

    [TestMethod]
    [DataRow("valid", 0)]
    [DataRow("none", 0)]
    [DataRow("nonzero", 17)]
    [DataRow("flood", 0)]
    [DataRow("stderr-flood", 0)]
    public void RunDotnetupProcess_CapturesBothStreamsAndExitCode(string mode, int expectedExitCode)
    {
        using var files = new SelfUpdateTestFiles(mode: mode);

        var result = DotnetupTestUtilities.RunDotnetupProcess(
            ["--version"], captureOutput: true, executablePath: files.Paths.InstalledPath,
            timeout: TimeSpan.FromSeconds(20));

        Assert.AreEqual(expectedExitCode, result.exitCode);
        Assert.Contains(SelfUpdateTestFiles.OriginalVersion + Environment.NewLine, result.output);
        Assert.Contains($"SelfUpdateProcess mode: {mode}", result.output);
        if (mode is "flood" or "stderr-flood")
        {
            Assert.Contains(new string('x', 4096 * 128), result.output);
        }
    }

    [TestMethod]
    public void RunDotnetupProcess_WithoutCaptureReturnsExitCodeAndEmptyOutput()
    {
        using var files = new SelfUpdateTestFiles();

        var result = DotnetupTestUtilities.RunDotnetupProcess(
            ["--unknown"], executablePath: files.Paths.InstalledPath, timeout: TimeSpan.FromSeconds(20));

        Assert.AreEqual(91, result.exitCode);
        Assert.AreEqual(string.Empty, result.output);
    }

    [TestMethod]
    public void RunDotnetupProcess_PreservesInteractiveInput()
    {
        using var files = new SelfUpdateTestFiles();

        var result = DotnetupTestUtilities.RunDotnetupProcess(
            ["--hold"], captureOutput: true, standardInput: "done" + Environment.NewLine,
            executablePath: files.Paths.InstalledPath, timeout: TimeSpan.FromSeconds(20));

        Assert.AreEqual(0, result.exitCode);
        Assert.AreEqual(string.Concat(Enumerable.Repeat(SelfUpdateTestFiles.OriginalVersion + Environment.NewLine, 2)), result.output);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void RunDotnetupProcess_TimeoutTerminatesChild(bool captureOutput, bool redirectInput)
    {
        using var files = new SelfUpdateTestFiles(mode: "timeout");

        var exception = Assert.ThrowsExactly<TimeoutException>(() => DotnetupTestUtilities.RunDotnetupProcess(
            ["--version"], captureOutput: captureOutput, executablePath: files.Paths.InstalledPath,
            standardInput: redirectInput ? string.Empty : null,
            timeout: TimeSpan.FromSeconds(5)));

        Assert.Contains("did not exit within", exception.Message);
        var processId = int.Parse(File.ReadAllText(files.Paths.InstalledPath + ".pid"), System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            using var process = Process.GetProcessById(processId);
            Assert.IsTrue(process.WaitForExit(10_000), "Timed-out dotnetup process must be terminated.");
        }
        catch (ArgumentException)
        {
            // The terminated child may already have been reaped.
        }
    }
}
