// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Commands.Workload.Install.WorkloadInstallRecords;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.Win32;

namespace Microsoft.DotNet.Cli.Tests;

/// <summary>
///  Tests for <see cref="WorkloadInstallDetector"/>, the read-only workload install detection used by
///  the AOT first-run path. Exercises the file-based record layout in a temporary dotnet root so the
///  detection runs without NuGet, MSBuild, or installer-IPC dependencies.
/// </summary>
[TestClass]
public class WorkloadInstallDetectorTests
{
    private static string CurrentFeatureBand => new SdkFeatureBand(Product.Version).ToString();

    private static string CreateTempDotnetDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "aot-wl-detect-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [TestMethod]
    public void NoWorkloadRecords_ReturnsFalse()
    {
        string dotnetDir = CreateTempDotnetDir();
        try
        {
            Assert.IsFalse(WorkloadInstallDetector.HasInstalledWorkloadsForCurrentBand(dotnetDir));
        }
        finally
        {
            Directory.Delete(dotnetDir, recursive: true);
        }
    }

    [TestMethod]
    public void FileBasedWorkloadRecordPresent_ReturnsTrue()
    {
        string dotnetDir = CreateTempDotnetDir();
        try
        {
            // Mirrors FileBasedInstallationRecordRepository:
            // {dotnetDir}/metadata/workloads/{featureBand}/InstalledWorkloads/{workloadId}
            string installedWorkloadsDir = Path.Combine(
                dotnetDir, "metadata", "workloads", CurrentFeatureBand, "InstalledWorkloads");
            Directory.CreateDirectory(installedWorkloadsDir);
            File.WriteAllText(Path.Combine(installedWorkloadsDir, "microsoft.net.sdk.test-workload"), "");

            Assert.IsTrue(WorkloadInstallDetector.HasInstalledWorkloadsForCurrentBand(dotnetDir));
        }
        finally
        {
            Directory.Delete(dotnetDir, recursive: true);
        }
    }

    [TestMethod]
    public void EmptyInstalledWorkloadsDirectory_ReturnsFalse()
    {
        string dotnetDir = CreateTempDotnetDir();
        try
        {
            // Directory exists but contains no records - nothing is installed.
            Directory.CreateDirectory(Path.Combine(
                dotnetDir, "metadata", "workloads", CurrentFeatureBand, "InstalledWorkloads"));

            Assert.IsFalse(WorkloadInstallDetector.HasInstalledWorkloadsForCurrentBand(dotnetDir));
        }
        finally
        {
            Directory.Delete(dotnetDir, recursive: true);
        }
    }

    /// <summary>
    ///  Exercises the registry read path that is enabled under CLI_AOT in
    ///  <see cref="RegistryWorkloadInstallationRecordRepository"/>. Uses the read-only testing
    ///  constructor against HKCU so the test does not require elevation or touch HKLM.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void RegistryRecordPresent_GetInstalledWorkloads_ReturnsWorkload()
    {
        var featureBand = new SdkFeatureBand(Product.Version);
        string basePath = $@"Software\Microsoft\dotnet-aot-tests\{Guid.NewGuid():N}";
        try
        {
            using (RegistryKey recordKey = Registry.CurrentUser.CreateSubKey(
                Path.Combine(basePath, $"{featureBand}", "microsoft.net.sdk.test-workload")))
            {
                Assert.IsNotNull(recordKey);
            }

            var repository = new RegistryWorkloadInstallationRecordRepository(Registry.CurrentUser, basePath);

            var installed = repository.GetInstalledWorkloads(featureBand).ToList();
            Assert.HasCount(1, installed);
            Assert.AreEqual("microsoft.net.sdk.test-workload", installed[0].ToString());

            Assert.Contains(
                featureBand.ToString(),
                repository.GetFeatureBandsWithInstallationRecords().Select(b => b.ToString()).ToList());
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(basePath, throwOnMissingSubKey: false);
        }
    }
}
