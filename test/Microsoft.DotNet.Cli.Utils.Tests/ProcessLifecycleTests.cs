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

    private void VerifyCancellationSignal(
        PosixSignal signal,
        int expectedForcedExitCode)
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory();

        using (Process cooperativeChild = StartChild(
            "cooperative",
            testDirectory.Path))
        {
            WaitForMarker(testDirectory.Path, "ready");
            cooperativeChild.SafeHandle.Signal(signal).Should().BeTrue();
            WaitForMarker(testDirectory.Path, "cancelled");
            cooperativeChild.WaitForExit(s_waitTimeout).Should().BeTrue();
            cooperativeChild.ExitCode.Should().Be(0);
        }

        using (Process uncooperativeChild = StartChild(
            "uncooperative",
            testDirectory.Path))
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
        Process process = CreateChildProcess(childMode, testDirectory);
        process.Start();
        return process;
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
                () => File.Exists(markerPath) && File.ReadAllText(markerPath) == expectedValue,
                s_waitTimeout)
            .Should().BeTrue($"the child process should write '{expectedValue}' to the signal marker");
    }
}
