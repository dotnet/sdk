// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Installer.Windows;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Moq;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests;

[TestClass]
[OSCondition(OperatingSystems.Windows)]
[SupportedOSPlatform("windows")]
public class GivenMsiCancellation : SdkTest
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void CancellationAfterInstallationStartsStillRollsBack(bool manifest, bool cancelDuringInstallation)
    {
        string directory = TestAssetsManager.CreateTestDirectory().Path;
        using CancellationTokenSource cancellation = new();
        using TimestampedFileLogger logger = new(Path.Combine(directory, "install.log"));
        var resolver = new Mock<IWorkloadResolver>();
        WorkloadId workloadId = new("test-workload");
        WorkloadPackId packId = new("test-pack");
        resolver.Setup(r => r.GetInstalledManifests()).Returns([]);
        resolver.Setup(r => r.GetPacksInWorkload(workloadId)).Returns([packId]);
        resolver.Setup(r => r.TryGetPackInfo(packId)).Returns(new PackInfo(
            packId, "1.0.0", WorkloadPackKind.Sdk, directory, "test-pack"));
        var installer = new TestInstaller(directory, logger, resolver.Object);
        var originalCancellation = new OperationCanceledException(cancellation.Token);
        if (cancelDuringInstallation)
        {
            installer.OnInstall = () =>
            {
                cancellation.Cancel();
                throw originalCancellation;
            };
        }

        try
        {
            Action install = () => CliTransaction.RunNew(context =>
            {
                if (manifest)
                {
                    installer.InstallWorkloadManifest(
                        new ManifestVersionUpdate(new ManifestId("test-manifest"), new ManifestVersion("1.0.0"), "6.0.100"),
                        context, cancellation.Token);
                }
                else
                {
                    installer.InstallWorkloads([workloadId], new SdkFeatureBand("6.0.100"), context, cancellation.Token);
                }

                installer.Actions.Should().Equal(InstallAction.Install);
                cancellation.Cancel();
                throw originalCancellation;
            });

            install.Should().Throw<OperationCanceledException>().Which.Should().BeSameAs(originalCancellation);
            installer.Actions.Should().Equal(InstallAction.Install, InstallAction.Uninstall);
            installer.Installed.Should().BeFalse();
            installer.PayloadTokens.Should().HaveCount(2);
            installer.PayloadTokens[0].Should().Be(cancellation.Token);
            installer.PayloadTokens[1].CanBeCanceled.Should().BeTrue();
            installer.PayloadTokens[1].Should().NotBe(cancellation.Token);
            installer.PayloadTokens[1].IsCancellationRequested.Should().BeFalse();
        }
        finally
        {
            installer.Shutdown();
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CancellationWithoutAManifestInstallationPreservesTheExistingManifest(bool precancelled)
    {
        string directory = TestAssetsManager.CreateTestDirectory().Path;
        using CancellationTokenSource cancellation = new();
        using TimestampedFileLogger logger = new(Path.Combine(directory, "install.log"));
        var installer = new TestInstaller(directory, logger, new Mock<IWorkloadResolver>().Object, installed: true);
        var originalCancellation = new OperationCanceledException(cancellation.Token);
        if (precancelled)
        {
            cancellation.Cancel();
        }

        try
        {
            Action install = () => CliTransaction.RunNew(context =>
            {
                installer.InstallWorkloadManifest(
                    new ManifestVersionUpdate(new ManifestId("test-manifest"), new ManifestVersion("1.0.0"), "6.0.100"),
                    context, cancellation.Token);
                installer.Actions.Should().Equal(InstallAction.None);
                cancellation.Cancel();
                throw originalCancellation;
            });

            var exception = install.Should().Throw<OperationCanceledException>().Which;
            exception.CancellationToken.Should().Be(cancellation.Token);
            if (precancelled)
            {
                installer.Actions.Should().BeEmpty();
                installer.PayloadTokens.Should().BeEmpty();
            }
            else
            {
                exception.Should().BeSameAs(originalCancellation);
                installer.Actions.Should().Equal(InstallAction.None);
                installer.PayloadTokens.Should().ContainSingle();
            }

            installer.Installed.Should().BeTrue();
        }
        finally
        {
            installer.Shutdown();
        }
    }

    private sealed class TestInstaller : NetSdkMsiInstallerClient
    {
        private readonly MsiPayload _payload;
        public List<InstallAction> Actions { get; } = [];
        public List<CancellationToken> PayloadTokens { get; } = [];
        public bool Installed { get; private set; }
        public Action? OnInstall { get; set; }

        public TestInstaller(string directory, ISetupLogger logger, IWorkloadResolver resolver, bool installed = false)
            : base(new TestElevationContext(), logger, verifyMsiSignature: false, resolver, new SdkFeatureBand("6.0.100"),
                new MockNuGetPackageDownloader(), reporter: NullReporter.Instance)
        {
            Installed = installed;
            string manifestPath = Path.Combine(directory, "msi.json");
            File.WriteAllText(manifestPath, $$"""
                {"Payload":"test.msi","ProductCode":"{{Guid.NewGuid():B}}","ProductVersion":"1.0.0","ProviderKeyName":"{{Guid.NewGuid():B}}"}
                """);
            _payload = new MsiPayload(manifestPath, Path.Combine(directory, "test.msi"));
        }

        protected override MsiPayload GetCachedMsiPayload(
            string packageId, string packageVersion, CancellationToken cancellationToken, DirectoryPath? offlineCache)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PayloadTokens.Add(cancellationToken);
            return _payload;
        }

        protected override void ValidateMsiDatabase(MsiPayload msiPayload) { }

        protected override DetectState DetectPackage(string productCode, out Version installedVersion)
        {
            installedVersion = new Version("1.0.0");
            return Installed ? DetectState.Present : DetectState.Absent;
        }

        protected override void ExecutePackage(MsiPayload msi, InstallAction action, string? displayName = null)
        {
            Actions.Add(action);
            if (action == InstallAction.Install)
            {
                Installed = true;
                OnInstall?.Invoke();
            }
            else if (action == InstallAction.Uninstall)
            {
                Installed = false;
            }
        }

        protected override void UpdateDependent(InstallRequestType requestType, string providerKeyName, string dependent) { }
    }

    private sealed class TestElevationContext : InstallElevationContextBase
    {
        public override bool IsClient => true;
        public override bool IsElevated => false;
        public override void Elevate() => throw new InvalidOperationException("The test must not elevate.");
    }
}
