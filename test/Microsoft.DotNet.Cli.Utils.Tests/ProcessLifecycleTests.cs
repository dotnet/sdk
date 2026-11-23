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
    [OSCondition(OperatingSystems.Windows)]
    public void ConsoleCtrlCCancelsTokenAndForcesTerminationWhenCancellationIsIgnored()
        => VerifyCancellationSignal(PosixSignal.SIGINT, expectedForcedExitCode: 130);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void RepeatedSigIntTerminatesAnUncooperativeProcess()
        => VerifyRepeatedSignal(PosixSignal.SIGINT, expectedExitCode: 130);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void RepeatedConsoleCtrlCTerminatesAnUncooperativeProcess()
        => VerifyRepeatedSignal(PosixSignal.SIGINT, expectedExitCode: 130);

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
    public void SoftCancellationTimeoutAllowsForcefulCancellationToComplete()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();
        using Process child = StartChild("forceful-cooperative", testDirectory.Path);

        WaitForMarker(testDirectory.Path, "ready");
        SendSignal(child, testDirectory.Path, PosixSignal.SIGINT);
        WaitForMarker(testDirectory.Path, "forceful-cancelled");
        child.WaitForExit(s_waitTimeout).Should().BeTrue();

        child.ExitCode.Should().Be(130);
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
    public void ManagedCliWaitsForSecondSigIntBeforeTerminatingExternalCommand()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();
        string markerPath = GetMarkerPath(testDirectory.Path);
        string commandPath = Path.Combine(testDirectory.Path, "dotnet-cancellation-test");
        File.WriteAllText(
            commandPath,
            $"#!/bin/sh{Environment.NewLine}printf ready > '{markerPath}'{Environment.NewLine}sleep 30{Environment.NewLine}");
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
            WorkingDirectory = testDirectory.Path,
        };
        startInfo.ArgumentList.Add("cancellation-test");
        var testEnvironment = new Dictionary<string, string?>();
        SdkTestContext.Current.AddTestEnvironmentVariables(testEnvironment);
        foreach ((string name, string? value) in testEnvironment)
        {
            startInfo.Environment[name] = value;
        }

        startInfo.Environment["DOTNET_CLI_ENABLEAOT"] = "0";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = bool.FalseString;
        startInfo.Environment["DOTNET_NOLOGO"] = bool.TrueString;
        startInfo.Environment["DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK"] = bool.TrueString;
        startInfo.Environment["PATH"] =
            testDirectory.Path + Path.PathSeparator + startInfo.Environment["PATH"];

        using Process child = new() { StartInfo = startInfo };
        bool childStarted = false;
        try
        {
            child.Start();
            childStarted = true;
            Task<string> stdoutTask = child.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
            Task<string> stderrTask = child.StandardError.ReadToEndAsync(TestContext.CancellationToken);

            if (!TryWaitForMarker(testDirectory.Path, "ready"))
            {
                if (!child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                }

                child.WaitForExit();
                Assert.Fail(
                    $"The managed CLI did not start the external command.{Environment.NewLine}" +
                    $"Exit code: {child.ExitCode}{Environment.NewLine}" +
                    $"Standard output:{Environment.NewLine}{stdoutTask.GetAwaiter().GetResult()}{Environment.NewLine}" +
                    $"Standard error:{Environment.NewLine}{stderrTask.GetAwaiter().GetResult()}");
            }

            child.SafeHandle.Signal(PosixSignal.SIGINT).Should().BeTrue();
            Thread.Sleep(TimeSpan.FromMilliseconds(750));
            child.HasExited.Should().BeFalse("the first SIGINT should allow the external command to finish");

            Stopwatch stopwatch = Stopwatch.StartNew();
            child.SafeHandle.Signal(PosixSignal.SIGINT).Should().BeTrue();
            child.WaitForExit(s_waitTimeout).Should().BeTrue();

            child.ExitCode.Should().Be(130);
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
            string standardError = stderrTask.GetAwaiter().GetResult();
            standardError.Should().NotContain("Unhandled exception");
            standardError.Should().NotContain("The operation was canceled");
            _ = stdoutTask.GetAwaiter().GetResult();
        }
        finally
        {
            if (childStarted && !child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                child.WaitForExit();
            }
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ForwardedMSBuildUsesTheBuildSoftCancellationTimeout()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();
        string msbuildPath = typeof(ProcessLifecycleTestApp).Assembly.Location;

        using Process child = StartChild("msbuild-external", testDirectory.Path, msbuildPath);
        try
        {
            WaitForMarker(testDirectory.Path, "ready");
            Stopwatch stopwatch = Stopwatch.StartNew();
            child.SafeHandle.Signal(PosixSignal.SIGINT).Should().BeTrue();
            child.WaitForExit(TimeSpan.FromSeconds(5)).Should().BeTrue();

            child.ExitCode.Should().Be(130);
            stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(1.5));
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(4));
        }
        finally
        {
            StopChild(child);
        }
    }

    [TestMethod]
    public void MtpLifecycleAllowsCooperativeCleanupAndKillsChildrenOnForcefulCancellation()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();
        using Process child = StartChild("mtp-lifecycle", testDirectory.Path);
        try
        {
            WaitForMarker(testDirectory.Path, "ready");
            int testApplicationProcessId =
                int.Parse(File.ReadAllText(GetMarkerPath(testDirectory.Path) + ".pid"));

            SendSignal(child, testDirectory.Path, PosixSignal.SIGINT);
            WaitForMarker(testDirectory.Path, "signal-cancelled");
            Thread.Sleep(TimeSpan.FromMilliseconds(750));
            child.HasExited.Should().BeFalse(
                "the first Ctrl+C should allow MTP test applications to finish reporting");
            Process.GetProcessById(testApplicationProcessId).HasExited.Should().BeFalse(
                "cooperative cancellation should not kill the test application");

            Stopwatch stopwatch = Stopwatch.StartNew();
            SendSignal(child, testDirectory.Path, PosixSignal.SIGINT, sequence: 2);
            child.WaitForExit(s_waitTimeout).Should().BeTrue();

            child.ExitCode.Should().Be(130);
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
            SpinWait.SpinUntil(
                    () => HasProcessExited(testApplicationProcessId),
                    TimeSpan.FromSeconds(10))
                .Should().BeTrue("forceful cancellation should kill the test application");
        }
        finally
        {
            StopChild(child);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void MtpLifecycleForwardsSigTermToTestApplication()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();
        using Process child = StartChild("mtp-sigterm-lifecycle", testDirectory.Path);
        try
        {
            WaitForMarker(testDirectory.Path, "ready");
            int testApplicationProcessId =
                int.Parse(File.ReadAllText(GetMarkerPath(testDirectory.Path) + ".pid"));

            Stopwatch stopwatch = Stopwatch.StartNew();
            child.SafeHandle.Signal(PosixSignal.SIGTERM).Should().BeTrue();
            WaitForMarker(testDirectory.Path, "forwarded");
            child.WaitForExit(s_waitTimeout).Should().BeTrue();

            child.ExitCode.Should().Be(143);
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
            SpinWait.SpinUntil(
                    () => HasProcessExited(testApplicationProcessId),
                    TimeSpan.FromSeconds(10))
                .Should().BeTrue("SIGTERM should let the test application exit before forceful cleanup");
        }
        finally
        {
            StopChild(child);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ForwardSigTermSkipsDisposedProcessesAndTerminatesRemainingProcesses()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();
        using Process child = StartChild("signal-child", testDirectory.Path);
        using var disposedProcess = new Process();
        disposedProcess.Dispose();

        try
        {
            WaitForMarker(testDirectory.Path, "grandchild-ready");

            ProcessReaper.ForwardSigTermAndTerminate([disposedProcess, child]);

            WaitForMarker(testDirectory.Path, "forwarded");
            child.WaitForExit(s_waitTimeout).Should().BeTrue();
            child.ExitCode.Should().Be(43);
        }
        finally
        {
            StopChild(child);
        }
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
        Stopwatch stopwatch = Stopwatch.StartNew();
        uncooperativeReaperParent.SafeHandle.Signal(PosixSignal.SIGTERM).Should().BeTrue();
        uncooperativeReaperParent.WaitForExit(s_waitTimeout).Should().BeTrue();
        uncooperativeReaperParent.ExitCode.Should().NotBe(0);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void LifecycleSigTermExitCodeIsNotReplacedByReapedChild()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();

        using Process reaperParent = StartChild(
            "lifecycle-reaper-parent",
            testDirectory.Path);

        WaitForMarker(testDirectory.Path, "reaper-ready");
        reaperParent.SafeHandle.Signal(PosixSignal.SIGTERM).Should().BeTrue();
        WaitForMarker(testDirectory.Path, "forwarded");
        reaperParent.WaitForExit(s_waitTimeout).Should().BeTrue();
        reaperParent.ExitCode.Should().Be(143);
    }

    private void VerifyCancellationSignal(PosixSignal signal, int expectedForcedExitCode)
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();

        using (Process cooperativeChild = StartChild("cooperative", testDirectory.Path))
        {
            try
            {
                WaitForMarker(testDirectory.Path, "ready");
                SendSignal(cooperativeChild, testDirectory.Path, signal);
                WaitForMarker(testDirectory.Path, "signal-cancelled");
                cooperativeChild.WaitForExit(s_waitTimeout).Should().BeTrue();
                cooperativeChild.ExitCode.Should().Be(130);
            }
            finally
            {
                StopChild(cooperativeChild);
            }
        }

        using (Process uncooperativeChild = StartChild("uncooperative", testDirectory.Path))
        {
            try
            {
                WaitForMarker(testDirectory.Path, "ready");
                SendSignal(uncooperativeChild, testDirectory.Path, signal);
                uncooperativeChild.WaitForExit(s_waitTimeout).Should().BeTrue();
                uncooperativeChild.ExitCode.Should().Be(expectedForcedExitCode);
            }
            finally
            {
                StopChild(uncooperativeChild);
            }
        }
    }

    private void VerifyRepeatedSignal(PosixSignal signal, int expectedExitCode)
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();
        using Process child = StartChild("repeated", testDirectory.Path);
        try
        {
            WaitForMarker(testDirectory.Path, "ready");
            SendSignal(child, testDirectory.Path, signal);
            WaitForMarker(testDirectory.Path, "signal-cancelled");
            Stopwatch stopwatch = Stopwatch.StartNew();
            SendSignal(child, testDirectory.Path, signal, sequence: 2);
            child.WaitForExit(s_waitTimeout).Should().BeTrue();
            child.ExitCode.Should().Be(expectedExitCode);
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        }
        finally
        {
            StopChild(child);
        }
    }

    private static Process StartChild(string childMode, string testDirectory, params string[] additionalArguments)
    {
        string markerPath = GetMarkerPath(testDirectory);
        File.Delete(markerPath);
        File.Delete(markerPath + ".signal-1");
        File.Delete(markerPath + ".signal-2");
        Process process = CreateChildProcess(childMode, testDirectory, additionalArguments);
        process.Start();
        return process;
    }

    private static void SendSignal(Process child, string testDirectory, PosixSignal signal, int sequence = 1)
    {
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(Path.Combine(testDirectory, $"signal-marker.signal-{sequence}"), "");
        }
        else
        {
            child.SafeHandle.Signal(signal).Should().BeTrue();
        }
    }

    private static void StopChild(Process child)
    {
        if (!child.HasExited)
        {
            child.Kill(entireProcessTree: true);
            child.WaitForExit(s_waitTimeout).Should().BeTrue();
        }
    }

    private static Process CreateChildProcess(
        string childMode,
        string testDirectory,
        params string[] additionalArguments)
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
        foreach (string argument in additionalArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new Process { StartInfo = startInfo };
    }

    private static void WaitForMarker(string testDirectory, string expectedValue)
    {
        TryWaitForMarker(testDirectory, expectedValue)
            .Should().BeTrue($"the child process should write '{expectedValue}' to the signal marker");
    }

    private static bool TryWaitForMarker(string testDirectory, string expectedValue)
    {
        string markerPath = GetMarkerPath(testDirectory);
        return SpinWait.SpinUntil(() => MarkerHasValue(markerPath, expectedValue), s_waitTimeout);
    }

    private static bool MarkerHasValue(string markerPath, string expectedValue)
    {
        try
        {
            if (!File.Exists(markerPath))
            {
                return false;
            }

            using FileStream stream = new(markerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new(stream);
            return reader.ReadToEnd() == expectedValue;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static string GetMarkerPath(string testDirectory)
        => Path.Combine(testDirectory, "signal-marker");

    private static bool HasProcessExited(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }
}
