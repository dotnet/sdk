// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using ManifestReaderTests;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Tests;

[TestClass]
public class WorkloadInfoHelperTests : SdkTest
{
    private const string SdkVersion = "6.0.100";

    [TestMethod]
    public void InstallationRecordsAreReadFromTheDotnetRoot()
    {
        var (dotnetRoot, userProfileDir) = CreateLayout();
        WriteInstallationRecord(dotnetRoot, "mock-1");

        var helper = CreateHelper(dotnetRoot, userProfileDir);

        helper.InstalledSdkWorkloadIds.Should().Equal(new WorkloadId("mock-1"));
    }

    [TestMethod]
    public void UserLocalInstallationRecordsAreReadFromTheUserProfile()
    {
        var (dotnetRoot, userProfileDir) = CreateLayout();
        var featureBandMetadataDir = Path.Combine(dotnetRoot, "metadata", "workloads", SdkVersion);
        Directory.CreateDirectory(featureBandMetadataDir);
        File.WriteAllText(Path.Combine(featureBandMetadataDir, "userlocal"), string.Empty);
        WriteInstallationRecord(userProfileDir, "mock-2");

        var helper = CreateHelper(dotnetRoot, userProfileDir);

        helper.InstalledSdkWorkloadIds.Should().Equal(new WorkloadId("mock-2"));
    }

    [TestMethod]
    public void ReadingInstalledWorkloadsDoesNotCreateTheInstaller()
    {
        // `dotnet --info`, `dotnet workload --info` and `dotnet new` only read installation records. Creating the
        // installer there left a temp directory behind on every run, so it must only be created when it's used.
        var (dotnetRoot, userProfileDir) = CreateLayout();
        WriteInstallationRecord(dotnetRoot, "mock-1");

        var helper = CreateHelper(dotnetRoot, userProfileDir);

        helper.InstalledAndExtendedWorkloads.Select(_ => _.Id.ToString()).Should().Contain("mock-1");
        var installer = (Lazy<IInstaller>)typeof(WorkloadInfoHelper)
            .GetField("_installer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(helper)!;
        installer.IsValueCreated.Should().BeFalse();
    }

    private (string DotnetRoot, string UserProfileDir) CreateLayout()
    {
        var testDirectory = TestAssetsManager.CreateTestDirectory().Path;
        return (Path.Combine(testDirectory, "dotnet"), Path.Combine(testDirectory, "user-profile"));
    }

    private static void WriteInstallationRecord(string workloadRootDir, string workloadId)
    {
        var recordDir = Path.Combine(workloadRootDir, "metadata", "workloads", SdkVersion, "InstalledWorkloads");
        Directory.CreateDirectory(recordDir);
        File.WriteAllText(Path.Combine(recordDir, workloadId), string.Empty);
    }

    private WorkloadInfoHelper CreateHelper(string dotnetRoot, string userProfileDir)
    {
        var manifestPath = Path.Combine(TestAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "MockWorkloadsSample.json");
        var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider([manifestPath]), dotnetRoot, userProfileDir: userProfileDir);

        return new WorkloadInfoHelper(
            isInteractive: false,
            currentSdkVersion: SdkVersion,
            dotnetDir: dotnetRoot,
            userProfileDir: userProfileDir,
            workloadResolver: workloadResolver);
    }
}
