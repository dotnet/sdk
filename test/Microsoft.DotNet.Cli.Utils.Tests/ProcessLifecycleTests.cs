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
    public void SigTermCancelsTokenAndForcesTerminationWhenCancellationIsIgnored()
        => VerifyCancellationSignal(
            PosixSignal.SIGTERM,
            expectedForcedExitCode: 143);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void SigIntCancelsTokenAndForcesTerminationWhenCancellationIsIgnored()
        => VerifyCancellationSignal(
            PosixSignal.SIGINT,
            expectedForcedExitCode: 130);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ConsoleCtrlCCancelsTokenAndForcesTerminationWhenCancellationIsIgnored()
        => VerifyCancellationSignal(PosixSignal.SIGINT, expectedForcedExitCode: 130);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow(PosixSignal.SIGINT, 130)]
    [DataRow(PosixSignal.SIGTERM, 143)]
    public void RepeatedUnixSignalsTerminateAnUncooperativeProcess(PosixSignal signal, int exitCode)
        => VerifyRepeatedSignal(signal, exitCode);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void RepeatedConsoleCtrlCTerminatesAnUncooperativeProcess()
        => VerifyRepeatedSignal(PosixSignal.SIGINT, unchecked((int)0xC000013A));

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ConsoleCtrlCDoesNotCancelWhileAReaperSuppressionLeaseIsActive()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();
        using Process child = StartChild("suppressed", testDirectory.Path);
        try
        {
            WaitForMarker(testDirectory.Path, "ready");
            SendSignal(child, testDirectory.Path, PosixSignal.SIGINT);
            WaitForMarker(testDirectory.Path, "signal-suppressed");
            File.WriteAllText(Path.Combine(testDirectory.Path, "signal-marker.release"), "");
            child.WaitForExit(s_waitTimeout).Should().BeTrue();
            child.ExitCode.Should().Be(0);
        }
        finally
        {
            StopChild(child);
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
            SendSignal(child, testDirectory.Path, signal, sequence: 2);
            child.WaitForExit(s_waitTimeout).Should().BeTrue();
            child.ExitCode.Should().Be(expectedExitCode);
            // The second signal must use default termination, not merely wait for the first signal's exit timer.
            File.Exists(Path.Combine(testDirectory.Path, "signal-marker.managed-exit")).Should().BeFalse();
        }
        finally
        {
            StopChild(child);
        }
    }

    private void VerifyCancellationSignal(
        PosixSignal signal,
        int expectedForcedExitCode)
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();

        using (Process cooperativeChild = StartChild(
            "cooperative",
            testDirectory.Path))
        {
            try
            {
                WaitForMarker(testDirectory.Path, "ready");
                SendSignal(cooperativeChild, testDirectory.Path, signal);
                WaitForMarker(testDirectory.Path, "signal-cancelled");
                cooperativeChild.WaitForExit(s_waitTimeout).Should().BeTrue();
                cooperativeChild.ExitCode.Should().Be(0);
            }
            finally
            {
                StopChild(cooperativeChild);
            }
        }

        using (Process uncooperativeChild = StartChild(
            "uncooperative",
            testDirectory.Path))
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
        string markerPath = Path.Combine(testDirectory, "signal-marker");
        File.Delete(markerPath);
        File.Delete(markerPath + ".signal-1");
        File.Delete(markerPath + ".signal-2");
        File.Delete(markerPath + ".release");
        File.Delete(markerPath + ".managed-exit");
        Process process = CreateChildProcess(childMode, testDirectory);
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

    private static Process CreateChildProcess(string childMode, string testDirectory)
    {
        string markerPath = Path.Combine(testDirectory, "signal-marker");
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
        string markerPath = Path.Combine(testDirectory, "signal-marker");
        SpinWait.SpinUntil(
                () =>
                {
                    if (!File.Exists(markerPath))
                    {
                        return false;
                    }

                    using StreamReader reader = new(new FileStream(markerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                    return reader.ReadToEnd() == expectedValue;
                },
                s_waitTimeout)
            .Should().BeTrue($"the child process should write '{expectedValue}' to the signal marker");
    }
}
