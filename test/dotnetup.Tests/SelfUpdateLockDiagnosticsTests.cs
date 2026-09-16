// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SelfUpdateLockDiagnosticsTests : SdkTest
{
    private DirectoryInfo _directory = null!;

    [TestInitialize]
    public void Initialize() => _directory = Directory.CreateTempSubdirectory("self-update-lock-diagnostics-");

    [TestCleanup]
    public void Cleanup() => _directory.Delete(recursive: true);

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("\0")]
    public void InvalidPathReturnsEmpty(string? path)
    {
        Assert.AreEqual(string.Empty, SelfUpdateLockDiagnostics.Describe(path!));
    }

    [TestMethod]
    public void MissingFileReturnsEmptyWithoutCreatingFile()
    {
        var path = Path.Combine(_directory.FullName, "missing.lock");

        Assert.AreEqual(string.Empty, SelfUpdateLockDiagnostics.Describe(path));
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void DirectoryReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, SelfUpdateLockDiagnostics.Describe(_directory.FullName));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    public void UnsupportedPlatformReturnsEmptyForExistingFile()
    {
        var path = Path.Combine(_directory.FullName, "activity.lock");
        File.WriteAllText(path, "unchanged");

        Assert.AreEqual(string.Empty, SelfUpdateLockDiagnostics.Describe(path));
        Assert.AreEqual("unchanged", File.ReadAllText(path));
    }

    [TestMethod]
    public void NativeStructuresHaveWindowsLayoutWithoutManagedReferences()
    {
        Assert.IsFalse(RuntimeHelpers.IsReferenceOrContainsReferences<RestartManagerUniqueProcess>());
        Assert.IsFalse(RuntimeHelpers.IsReferenceOrContainsReferences<RestartManagerProcessInfo>());
        Assert.AreEqual(12, Unsafe.SizeOf<RestartManagerUniqueProcess>());
        Assert.AreEqual(668, Unsafe.SizeOf<RestartManagerProcessInfo>());
        Assert.AreEqual((nint)4, Marshal.OffsetOf<RestartManagerUniqueProcess>(nameof(RestartManagerUniqueProcess.ProcessStartTime)));
        Assert.AreEqual((nint)652, Marshal.OffsetOf<RestartManagerProcessInfo>(nameof(RestartManagerProcessInfo.ApplicationType)));
        Assert.AreEqual((nint)664, Marshal.OffsetOf<RestartManagerProcessInfo>(nameof(RestartManagerProcessInfo.Restartable)));
    }

    [TestMethod]
    [DataRow(0u)]
    [DataRow(uint.MaxValue)]
    public void InvalidProcessIdentityReturnsEmpty(uint processId)
    {
        Assert.AreEqual(string.Empty, SelfUpdateLockDiagnostics.DescribeProcess(new RestartManagerUniqueProcess { ProcessId = processId }));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ReusedProcessIdReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, SelfUpdateLockDiagnostics.DescribeProcess(new RestartManagerUniqueProcess { ProcessId = (uint)Environment.ProcessId }));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void MatchingProcessIdentityReportsOnlyNameAndPid()
    {
        using var process = Process.GetCurrentProcess();
        var startTime = process.StartTime.ToFileTimeUtc();
        var identity = new RestartManagerUniqueProcess
        {
            ProcessId = (uint)process.Id,
            ProcessStartTime = new FILETIME { dwLowDateTime = unchecked((int)startTime), dwHighDateTime = (int)(startTime >> 32) },
        };

        var description = SelfUpdateLockDiagnostics.DescribeProcess(identity);

        Assert.AreEqual(string.Create(CultureInfo.InvariantCulture, $"Lock holder: {process.ProcessName} (PID {process.Id})."), description);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void HeldFileReportsHolderOrEmptyAndLeavesLockIntact()
    {
        var path = Path.Combine(_directory.FullName, "private-file-name.lock");
        File.WriteAllText(path, "unchanged");
        using (var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var description = SelfUpdateLockDiagnostics.Describe(path);
            using var process = Process.GetCurrentProcess();
            var expected = string.Create(CultureInfo.InvariantCulture, $"Lock holder: {process.ProcessName} (PID {process.Id}).");

            Assert.IsTrue(description.Length == 0 || string.Equals(description, expected, StringComparison.Ordinal));
            Assert.ThrowsExactly<IOException>(() =>
            {
                using var competing = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            });
            Assert.AreEqual("unchanged".Length, held.Length);
        }

        Assert.AreEqual("unchanged", File.ReadAllText(path));
    }
}