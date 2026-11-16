// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils.TestApp;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.DotNet.Cli.Utils.Tests;

[TestClass]
public class ProcessLifecycleTests : SdkTest
{
    private static readonly TimeSpan s_waitTimeout = TimeSpan.FromSeconds(30);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void SigTermForcesTerminationWithoutCooperativeCancellation()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();
        using Process child = StartChild("cooperative", testDirectory.Path);

        WaitForMarker(testDirectory.Path, "ready");
        child.SafeHandle.Signal(PosixSignal.SIGTERM).Should().BeTrue();
        child.WaitForExit(s_waitTimeout).Should().BeTrue();
        child.ExitCode.Should().Be(143);
        File.ReadAllText(GetMarkerPath(testDirectory.Path)).Should().Be("ready");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void SigIntAllowsCooperativeCancellation()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();
        using Process child = StartChild("cooperative", testDirectory.Path);

        WaitForMarker(testDirectory.Path, "ready");
        child.SafeHandle.Signal(PosixSignal.SIGINT).Should().BeTrue();
        WaitForMarker(testDirectory.Path, "signal-cancelled");
        child.WaitForExit(s_waitTimeout).Should().BeTrue();
        child.ExitCode.Should().Be(130);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void SigIntForcesTerminationAfterSoftCancellationTimeout()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();
        using Process child = StartChild("uncooperative", testDirectory.Path);

        WaitForMarker(testDirectory.Path, "ready");
        Stopwatch stopwatch = Stopwatch.StartNew();
        child.SafeHandle.Signal(PosixSignal.SIGINT).Should().BeTrue();
        child.WaitForExit(s_waitTimeout).Should().BeTrue();

        child.ExitCode.Should().Be(130);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1.5));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void SoftCancellationTimeoutCanBeOverridden()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();
        using Process child = StartChild("extended-grace", testDirectory.Path);

        WaitForMarker(testDirectory.Path, "ready");
        child.SafeHandle.Signal(PosixSignal.SIGINT).Should().BeTrue();
        WaitForMarker(testDirectory.Path, "override-completed");
        child.WaitForExit(s_waitTimeout).Should().BeTrue();
        child.ExitCode.Should().Be(130);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void SecondSigIntBypassesSoftCancellationTimeoutOverride()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();
        using Process child = StartChild("extended-grace", testDirectory.Path);

        WaitForMarker(testDirectory.Path, "ready");
        child.SafeHandle.Signal(PosixSignal.SIGINT).Should().BeTrue();
        WaitForMarker(testDirectory.Path, "signal-cancelled");
        Stopwatch stopwatch = Stopwatch.StartNew();
        child.SafeHandle.Signal(PosixSignal.SIGINT).Should().BeTrue();
        child.WaitForExit(s_waitTimeout).Should().BeTrue();

        child.ExitCode.Should().Be(130);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        File.ReadAllText(GetMarkerPath(testDirectory.Path)).Should().Be("signal-cancelled");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void SigTermBypassesSoftCancellationTimeoutOverride()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();
        using Process child = StartChild("extended-grace", testDirectory.Path);

        WaitForMarker(testDirectory.Path, "ready");
        child.SafeHandle.Signal(PosixSignal.SIGTERM).Should().BeTrue();
        child.WaitForExit(s_waitTimeout).Should().BeTrue();

        child.ExitCode.Should().Be(143);
        File.ReadAllText(GetMarkerPath(testDirectory.Path)).Should().Be("ready");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ManagedCliReportsSigIntWithoutCancellationError()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();
        string markerPath = GetMarkerPath(testDirectory.Path);
        string commandPath = Path.Combine(testDirectory.Path, "dotnet-cancellation-test");
        File.WriteAllText(
            commandPath,
            $"#!/bin/sh{Environment.NewLine}echo ready > '{markerPath}'{Environment.NewLine}sleep 30{Environment.NewLine}");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                commandPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        ProcessStartInfo startInfo = new(SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("cancellation-test");
        startInfo.Environment["DOTNET_CLI_ENABLEAOT"] = "0";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        startInfo.Environment["PATH"] =
            testDirectory.Path + Path.PathSeparator + startInfo.Environment["PATH"];

        using Process child = new() { StartInfo = startInfo };
        child.Start();
        WaitForMarker(testDirectory.Path, "ready");
        child.SafeHandle.Signal(PosixSignal.SIGINT).Should().BeTrue();
        child.WaitForExit(s_waitTimeout).Should().BeTrue();

        child.ExitCode.Should().Be(130);
        string standardError = child.StandardError.ReadToEnd();
        standardError.Should().NotContain("Unhandled exception");
        standardError.Should().NotContain("The operation was canceled");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void SigTermIsForwardedToReapedChild()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();

        using Process reaperParent = StartChild(
            "reaper-parent",
            testDirectory.Path);

        WaitForMarker(testDirectory.Path, "reaper-ready");
        reaperParent.SafeHandle.Signal(PosixSignal.SIGTERM).Should().BeTrue();
        WaitForMarker(testDirectory.Path, "forwarded");
        reaperParent.WaitForExit(s_waitTimeout).Should().BeTrue();
        reaperParent.ExitCode.Should().Be(43);

        using Process uncooperativeReaperParent = StartChild(
            "uncooperative-reaper-parent",
            testDirectory.Path);

        WaitForMarker(testDirectory.Path, "reaper-ready");
        uncooperativeReaperParent.SafeHandle.Signal(PosixSignal.SIGTERM).Should().BeTrue();
        uncooperativeReaperParent.WaitForExit(s_waitTimeout).Should().BeTrue();
        uncooperativeReaperParent.ExitCode.Should().NotBe(0);
    }

    private static Process StartChild(string childMode, string testDirectory)
    {
        string markerPath = GetMarkerPath(testDirectory);
        File.Delete(markerPath);
        Process process = CreateChildProcess(childMode, testDirectory);
        process.Start();
        return process;
    }

    private static Process CreateChildProcess(string childMode, string testDirectory)
    {
        string markerPath = GetMarkerPath(testDirectory);
        string testAssemblyPath = typeof(ProcessLifecycleTests).Assembly.Location;
        string helperAssemblyPath = typeof(ProcessLifecycleTestApp).Assembly.Location;
        ProcessStartInfo startInfo = new(SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--runtimeconfig");
        startInfo.ArgumentList.Add(Path.ChangeExtension(testAssemblyPath, ".runtimeconfig.json"));
        startInfo.ArgumentList.Add("--depsfile");
        startInfo.ArgumentList.Add(Path.ChangeExtension(testAssemblyPath, ".deps.json"));
        startInfo.ArgumentList.Add(helperAssemblyPath);
        startInfo.ArgumentList.Add(childMode);
        startInfo.ArgumentList.Add(markerPath);
        startInfo.ArgumentList.Add(startInfo.FileName);
        startInfo.ArgumentList.Add(helperAssemblyPath);
        startInfo.ArgumentList.Add(Path.ChangeExtension(testAssemblyPath, ".runtimeconfig.json"));
        startInfo.ArgumentList.Add(Path.ChangeExtension(testAssemblyPath, ".deps.json"));

        return new Process { StartInfo = startInfo };
    }

    private static void WaitForMarker(string testDirectory, string expectedValue)
    {
        string markerPath = GetMarkerPath(testDirectory);
        SpinWait.SpinUntil(
                () => File.Exists(markerPath) && File.ReadAllText(markerPath) == expectedValue,
                s_waitTimeout)
            .Should().BeTrue($"the child process should write '{expectedValue}' to the signal marker");
    }

    private static string GetMarkerPath(string testDirectory)
        => Path.Combine(testDirectory, "signal-marker");
}
