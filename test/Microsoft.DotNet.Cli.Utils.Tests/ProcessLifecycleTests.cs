// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.DotNet.Cli.Utils.Tests;

[TestClass]
public class ProcessLifecycleTests : SdkTest
{
    private const string ChildModeEnvironmentVariable = "DOTNET_CLI_PROCESS_LIFECYCLE_TEST_CHILD";
    private const string MarkerPathEnvironmentVariable = "DOTNET_CLI_PROCESS_LIFECYCLE_TEST_MARKER";
    private static readonly TimeSpan s_waitTimeout = TimeSpan.FromSeconds(30);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void SigTermCancelsTokenAndForcesTerminationWhenCancellationIsIgnored()
        => VerifyCancellationSignal(
            PosixSignal.SIGTERM,
            expectedForcedExitCode: 143,
            nameof(SigTermCancelsTokenAndForcesTerminationWhenCancellationIsIgnored));

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void SigIntCancelsTokenAndForcesTerminationWhenCancellationIsIgnored()
        => VerifyCancellationSignal(
            PosixSignal.SIGINT,
            expectedForcedExitCode: 130,
            nameof(SigIntCancelsTokenAndForcesTerminationWhenCancellationIsIgnored));

    private void VerifyCancellationSignal(
        PosixSignal signal,
        int expectedForcedExitCode,
        string testMethodName)
    {
        if (Environment.GetEnvironmentVariable(ChildModeEnvironmentVariable) is { } childMode)
        {
            RunChild(childMode);
            return;
        }

        var testDirectory = TestAssetsManager.CreateTestDirectory();

        using (Process cooperativeChild = StartChild(
            "cooperative",
            testDirectory.Path,
            testMethodName))
        {
            WaitForMarker(testDirectory.Path, "ready");
            cooperativeChild.SafeHandle.Signal(signal).Should().BeTrue();
            WaitForMarker(testDirectory.Path, "cancelled");
            cooperativeChild.WaitForExit(s_waitTimeout).Should().BeTrue();
            cooperativeChild.ExitCode.Should().Be(0);
        }

        using (Process uncooperativeChild = StartChild(
            "uncooperative",
            testDirectory.Path,
            testMethodName))
        {
            WaitForMarker(testDirectory.Path, "ready");
            uncooperativeChild.SafeHandle.Signal(signal).Should().BeTrue();
            uncooperativeChild.WaitForExit(s_waitTimeout).Should().BeTrue();
            uncooperativeChild.ExitCode.Should().Be(expectedForcedExitCode);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void SigTermIsForwardedToReapedChild()
    {
        if (Environment.GetEnvironmentVariable(ChildModeEnvironmentVariable) is { } childMode)
        {
            RunChild(childMode);
            return;
        }

        var testDirectory = TestAssetsManager.CreateTestDirectory();

        using Process reaperParent = StartChild(
            "reaper-parent",
            testDirectory.Path,
            nameof(SigTermIsForwardedToReapedChild));

        WaitForMarker(testDirectory.Path, "reaper-ready");
        reaperParent.SafeHandle.Signal(PosixSignal.SIGTERM).Should().BeTrue();
        WaitForMarker(testDirectory.Path, "forwarded");
        reaperParent.WaitForExit(s_waitTimeout).Should().BeTrue();
        reaperParent.ExitCode.Should().Be(43);

        using Process uncooperativeReaperParent = StartChild(
            "uncooperative-reaper-parent",
            testDirectory.Path,
            nameof(SigTermIsForwardedToReapedChild));

        WaitForMarker(testDirectory.Path, "reaper-ready");
        uncooperativeReaperParent.SafeHandle.Signal(PosixSignal.SIGTERM).Should().BeTrue();
        uncooperativeReaperParent.WaitForExit(s_waitTimeout).Should().BeTrue();
        uncooperativeReaperParent.ExitCode.Should().NotBe(0);
    }

    private static void RunChild(string childMode)
    {
        string markerPath = Environment.GetEnvironmentVariable(MarkerPathEnvironmentVariable)!;

        if (childMode is "reaper-parent" or "uncooperative-reaper-parent")
        {
            RunReaperParent(markerPath, childMode);
            return;
        }

        if (childMode == "signal-child")
        {
            RunSignalChild(markerPath);
            return;
        }

        if (childMode == "ignoring-signal-child")
        {
            RunIgnoringSignalChild(markerPath);
            return;
        }

        CancellationToken cancellationToken = ProcessLifecycle.CancellationToken;
        File.WriteAllText(markerPath, "ready");

        if (childMode == "uncooperative")
        {
            Thread.Sleep(Timeout.Infinite);
        }

        cancellationToken.WaitHandle.WaitOne();
        File.WriteAllText(markerPath, "cancelled");
    }

    private static void RunReaperParent(string markerPath, string childMode)
    {
        string testDirectory = Path.GetDirectoryName(markerPath)!;
        using Process signalChild = CreateChildProcess(
            childMode == "reaper-parent" ? "signal-child" : "ignoring-signal-child",
            testDirectory,
            nameof(SigTermIsForwardedToReapedChild));
        using ProcessReaper reaper = ProcessReaper.Create(signalChild);

        signalChild.Start();
        reaper.NotifyProcessStarted();
        WaitForMarker(testDirectory, "grandchild-ready");
        File.WriteAllText(markerPath, "reaper-ready");
        Thread.Sleep(Timeout.Infinite);
    }

    private static void RunIgnoringSignalChild(string markerPath)
    {
        using PosixSignalRegistration registration = PosixSignalRegistration.Create(
            PosixSignal.SIGTERM,
            context => context.Cancel = true);

        File.WriteAllText(markerPath, "grandchild-ready");
        Thread.Sleep(Timeout.Infinite);
    }

    private static void RunSignalChild(string markerPath)
    {
        using PosixSignalRegistration registration = PosixSignalRegistration.Create(
            PosixSignal.SIGTERM,
            context =>
            {
                context.Cancel = true;
                File.WriteAllText(markerPath, "forwarded");
                Environment.Exit(43);
            });

        File.WriteAllText(markerPath, "grandchild-ready");
        Thread.Sleep(Timeout.Infinite);
    }

    private static Process StartChild(string childMode, string testDirectory, string testMethodName)
    {
        string markerPath = Path.Combine(testDirectory, "signal-marker");
        File.Delete(markerPath);
        Process process = CreateChildProcess(childMode, testDirectory, testMethodName);
        process.Start();
        return process;
    }

    private static Process CreateChildProcess(string childMode, string testDirectory, string testMethodName)
    {
        string markerPath = Path.Combine(testDirectory, "signal-marker");
        ProcessStartInfo startInfo = new(SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(typeof(ProcessLifecycleTests).Assembly.Location);
        startInfo.ArgumentList.Add("--filter");
        startInfo.ArgumentList.Add($"FullyQualifiedName={typeof(ProcessLifecycleTests).FullName}.{testMethodName}");
        startInfo.Environment[ChildModeEnvironmentVariable] = childMode;
        startInfo.Environment[MarkerPathEnvironmentVariable] = markerPath;

        return new Process { StartInfo = startInfo };
    }

    private static void WaitForMarker(string testDirectory, string expectedValue)
    {
        string markerPath = Path.Combine(testDirectory, "signal-marker");
        SpinWait.SpinUntil(
                () => File.Exists(markerPath) && File.ReadAllText(markerPath) == expectedValue,
                s_waitTimeout)
            .Should().BeTrue($"the child process should write '{expectedValue}' to the signal marker");
    }
}
