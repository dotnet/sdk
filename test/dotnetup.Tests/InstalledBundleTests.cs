// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.WindowsInstaller;
using Microsoft.Win32;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
[OSCondition(OperatingSystems.Windows)]
public class InstalledBundleTests
{
    private string _keyPath = null!;
    private RegistryKey _uninstallKey = null!;

    [TestInitialize]
    public void CreateKey()
    {
        _keyPath = $@"Software\dotnetup-tests\{Guid.NewGuid():N}";
        _uninstallKey = Registry.CurrentUser.CreateSubKey(_keyPath);
    }

    [TestCleanup]
    public void DeleteKey()
    {
        _uninstallKey.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
    }

    // Upgrade codes read from bundles installed by the official .NET installers.
    [TestMethod]
    [DataRow(InstallComponent.SDK, "10.0.404", "800DC81F-5309-5637-EBBF-2BBBF7F0CBD4")]
    [DataRow(InstallComponent.SDK, "10.0.115", "5EEC2050-5B99-6205-272D-20F397F974D7")]
    [DataRow(InstallComponent.SDK, "11.0.100-rc.3.26522.148", "D8343361-595C-5DDA-DA99-4450D7FAFADD")]
    [DataRow(InstallComponent.Runtime, "11.0.0-rc.3.26522.148", "9E12DBB0-5E37-4493-E5EB-64689F704C13")]
    [DataRow(InstallComponent.Runtime, "9.0.23", "246F1645-53A5-57B7-A2FD-41078EF81F4D")]
    [DataRow(InstallComponent.Runtime, "6.0.1", "B19A028F-5C89-7AF4-5102-0F8ED67CCACA")]
    [DataRow(InstallComponent.ASPNETCore, "9.0.23", "03B430C9-509A-3BB5-AFCF-AE9D088F61CF")]
    [DataRow(InstallComponent.ASPNETCore, "6.0.3", "B7C4CC53-CE40-39E9-9F43-81CE9F5CD47C")]
    public void BundleUpgradeCodes_MatchShippedBundles(InstallComponent component, string version, string expectedUpgradeCode)
    {
        BundleUpgradeCodes.For(component, new ReleaseVersion(version), InstallArchitecture.x64)
            .Should().Contain(new Guid(expectedUpgradeCode));
    }

    [TestMethod]
    public void ReadInstalledBundles_ReturnsOnlyBundlesInTheRequestedUpgradeFamily()
    {
        var requested = MakeSdk("10.0.400");
        RegisterBundle("Microsoft .NET SDK 10.0.404 (x64)", FamilyCode(requested));
        RegisterBundle("Microsoft .NET SDK 10.0.115 (x64)", FamilyCode(MakeSdk("10.0.100")));
        RegisterBundle("Microsoft .NET SDK 10.0.405 (arm64)", FamilyCode(MakeSdk("10.0.400", InstallArchitecture.arm64)));
        Register("Microsoft .NET SDK 10.0.401 (x64) from Visual Studio", upgradeCode: null);

        var bundles = DotnetEnvironmentManager.ReadInstalledBundles(_uninstallKey, requested);

        bundles.Should().ContainSingle().Which.Should().Be(
            new InstalledBundle(new DotnetInstall(new DotnetInstallRoot(string.Empty, InstallArchitecture.x64), new ReleaseVersion("10.0.404"), InstallComponent.SDK), "Microsoft .NET SDK 10.0.404 (x64)"));
    }

    [TestMethod]
    public void NewerBundleInTheFamily_RejectsDowngradeNamingTheBundle()
    {
        var requested = MakeSdk("10.0.403");
        RegisterBundle("Microsoft .NET SDK 10.0.404 (x64)", FamilyCode(requested));

        var check = () => InstallerOrchestratorSingleton.ThrowIfNewerVersionInstalled(
            requested, DotnetEnvironmentManager.ReadInstalledBundles(_uninstallKey, requested));

        var ex = check.Should().Throw<DotnetInstallException>().Which;
        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.NewerVersionInstalled);
        ex.Message.Should().Contain("Microsoft .NET SDK 10.0.404 (x64)");
    }

    [TestMethod]
    [DataRow("Microsoft .NET SDK 10.0.404 (x64)", "10.0.404")]
    [DataRow("Microsoft .NET SDK 11.0.100-rc.3.26522.148 (x64)", "11.0.100-rc.3.26522.148")]
    [DataRow("Microsoft .NET Runtime - 11.0.0 RC 3 (x64)", "11.0.0-rc.3")]
    [DataRow("Microsoft ASP.NET Core 10.0.15 - Shared Framework (x64)", "10.0.15")]
    [DataRow("Microsoft Windows Desktop Runtime - 10.0.15 (x64)", "10.0.15")]
    public void TryParseDisplayNameVersion_ReadsTheProductVersion(string displayName, string expectedVersion)
    {
        DotnetEnvironmentManager.TryParseDisplayNameVersion(displayName).Should().Be(new ReleaseVersion(expectedVersion));
    }

    private static DotnetInstall MakeSdk(string version, InstallArchitecture architecture = InstallArchitecture.x64) =>
        new(new DotnetInstallRoot(@"C:\Program Files\dotnet", architecture), new ReleaseVersion(version), InstallComponent.SDK);

    private static Guid FamilyCode(DotnetInstall install) =>
        BundleUpgradeCodes.For(install.Component, install.Version, install.InstallRoot.Architecture)[0];

    private void RegisterBundle(string displayName, Guid upgradeCode) => Register(displayName, upgradeCode);

    private void Register(string displayName, Guid? upgradeCode)
    {
        using var entry = _uninstallKey.CreateSubKey(Guid.NewGuid().ToString("B"));
        entry.SetValue("DisplayName", displayName);
        if (upgradeCode is { } code)
        {
            entry.SetValue("BundleUpgradeCode", new[] { code.ToString("B") }, RegistryValueKind.MultiString);
        }
    }
}
