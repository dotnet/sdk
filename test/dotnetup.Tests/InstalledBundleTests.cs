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
    [DataRow(InstallComponent.ASPNETCore, "10.0.0", "F9F0B753-5949-5D1B-1C78-83ACA8AFADC9")]
    [DataRow(InstallComponent.ASPNETCore, "9.0.23", "03B430C9-509A-3BB5-AFCF-AE9D088F61CF")]
    [DataRow(InstallComponent.ASPNETCore, "6.0.3", "B7C4CC53-CE40-39E9-9F43-81CE9F5CD47C")]
    [DataRow(InstallComponent.SDK, "3.1.426", "DBC75B24-53D3-4EF8-D856-2270E5044B1D")]
    [DataRow(InstallComponent.SDK, "5.0.408", "BF842004-5576-711C-6E2D-3A99F5260DAC")]
    [DataRow(InstallComponent.SDK, "6.0.428", "CCE07356-555E-6C5B-22FE-C92014353298")]
    [DataRow(InstallComponent.SDK, "7.0.410", "061B2A3A-5E6C-662D-BD2B-1420A1FC8C04")]
    [DataRow(InstallComponent.SDK, "8.0.404", "37A22C9B-5D0E-5AD5-D324-CAF7811A8A62")]
    [DataRow(InstallComponent.SDK, "9.0.100", "ACD82FB3-5046-609E-9088-708C1EE58F15")]
    [DataRow(InstallComponent.SDK, "9.0.300", "EBB5AA8B-5262-7DB7-A600-7835FD76A574")]
    [DataRow(InstallComponent.SDK, "10.0.100", "5EEC2050-5B99-6205-272D-20F397F974D7")]
    [DataRow(InstallComponent.Runtime, "3.1.32", "BB89595A-5975-5CF5-68F3-2F96E9BE88BF")]
    [DataRow(InstallComponent.Runtime, "5.0.17", "6D6E1D0E-58F3-4C1B-47B9-32F9A4A68D0B")]
    [DataRow(InstallComponent.Runtime, "6.0.36", "B19A028F-5C89-7AF4-5102-0F8ED67CCACA")]
    [DataRow(InstallComponent.Runtime, "7.0.20", "B2F208A0-591A-4224-C065-2C1FFA744D63")]
    [DataRow(InstallComponent.Runtime, "8.0.11", "8DA0C13F-57EB-417F-0238-B9ADB29DA0A2")]
    [DataRow(InstallComponent.Runtime, "8.0.11", "EE6C7C70-5F14-6F21-3023-B14D908CFEF6", InstallArchitecture.arm64)]
    [DataRow(InstallComponent.Runtime, "9.0.0", "246F1645-53A5-57B7-A2FD-41078EF81F4D")]
    [DataRow(InstallComponent.Runtime, "10.0.0", "F26E8E07-5A1A-54CA-FA89-99DAC5DB6C52")]
    [DataRow(InstallComponent.ASPNETCore, "5.0.17", "87FF09B1-99D6-3B69-B6E5-AAE6666CDE2F")]
    [DataRow(InstallComponent.ASPNETCore, "6.0.36", "B7C4CC53-CE40-39E9-9F43-81CE9F5CD47C")]
    [DataRow(InstallComponent.ASPNETCore, "7.0.20", "213055B6-E3CB-33D2-8DF2-18D3B8EBC013")]
    [DataRow(InstallComponent.ASPNETCore, "8.0.11", "BFF1C0F7-4682-3128-AF4A-2DEF21144BF1")]
    [DataRow(InstallComponent.ASPNETCore, "9.0.0", "03B430C9-509A-3BB5-AFCF-AE9D088F61CF")]
    [DataRow(InstallComponent.ASPNETCore, "10.0.0", "1D4B0CA6-5E29-4A6C-7653-3AEF4C8D627E", InstallArchitecture.x86)]
    [DataRow(InstallComponent.WindowsDesktop, "5.0.17", "695B6546-5CD6-7EE3-0917-5416920CF5C7")]
    [DataRow(InstallComponent.WindowsDesktop, "6.0.36", "E50DC420-5C2F-5516-1DA5-0F8B584F9E0D")]
    [DataRow(InstallComponent.WindowsDesktop, "7.0.20", "140FF8A9-5BC4-7778-1356-CE41D2A43CCF")]
    [DataRow(InstallComponent.WindowsDesktop, "8.0.11", "7F5F299F-5EB1-6FC0-6D86-FB7931E33C68")]
    [DataRow(InstallComponent.WindowsDesktop, "9.0.0", "914D2DAF-52FC-588C-CF9F-F9402797CB58")]
    [DataRow(InstallComponent.WindowsDesktop, "10.0.0", "90D77843-5FBD-64EF-6BDF-DF7574D01646")]
    [DataRow(InstallComponent.WindowsDesktop, "10.0.0", "6349E526-56E8-5C85-5E1D-47071794DCAA", InstallArchitecture.arm64)]
    public void BundleUpgradeCodes_MatchShippedBundles(InstallComponent component, string version, string expectedUpgradeCode, InstallArchitecture architecture = InstallArchitecture.x64)
    {
        BundleUpgradeCodes.For(component, new ReleaseVersion(version), architecture)
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
    [DataRow("Microsoft .NET Runtime - 10.0.0 Preview 7 (x64)", "10.0.0-preview.7")]
    [DataRow("Microsoft ASP.NET Core Runtime - 11.0.0 RC 3 (x64)", "11.0.0-rc.3")]
    [DataRow("Microsoft ASP.NET Core 10.0.15 - Shared Framework (x64)", "10.0.15")]
    [DataRow("Microsoft Windows Desktop Runtime - 10.0.15 (x64)", "10.0.15")]
    [DataRow("Microsoft Windows Desktop Runtime 11.0.0 (x64)", "11.0.0")]
    public void TryParseDisplayNameVersion_ReadsTheProductVersion(string displayName, string expectedVersion)
    {
        DotnetEnvironmentManager.TryParseDisplayNameVersion(displayName).Should().Be(new ReleaseVersion(expectedVersion));
    }

    // Display names and bundle versions written by the official installers.
    [TestMethod]
    [DataRow(InstallComponent.SDK, "Microsoft .NET SDK 11.0.100-rc.3.26522.148 (x64)", "11.1.26.52348", "11.0.100-rc.1.26425.128", true)]
    [DataRow(InstallComponent.Runtime, "Microsoft .NET Runtime - 11.0.0 RC 3 (x64)", "11.0.0.36622", "11.0.0-rc.1.26425.128", true)]
    [DataRow(InstallComponent.Runtime, "Microsoft .NET Runtime - 11.0.0 RC 3 (x64)", "11.0.0.36622", "11.0.0", false)]
    [DataRow(InstallComponent.Runtime, "Microsoft .NET Runtime - 10.0.0 Preview 7 (x64)", "10.0.0.35130", "10.0.0-rc.1.25451.107", false)]
    [DataRow(InstallComponent.ASPNETCore, "Microsoft ASP.NET Core Runtime - 11.0.0 RC 3 (x64)", "11.0.0.36622", "11.0.0-rc.1.26425.128", true)]
    [DataRow(InstallComponent.WindowsDesktop, "Microsoft Windows Desktop Runtime - 10.0.0 RC 2 (x64)", "10.0.0.35402", "10.0.0-rc.1.25451.107", true)]
    [DataRow(InstallComponent.WindowsDesktop, "Microsoft Windows Desktop Runtime 11.0.0 (x64)", "11.0.0.26381", "11.0.0-rc.3.26522.148", false)]
    [DataRow(InstallComponent.WindowsDesktop, "Microsoft Windows Desktop Runtime 11.0.0 (x64)", "11.0.0.26522", "11.0.0-rc.1.26425.128", true)]
    [DataRow(InstallComponent.WindowsDesktop, "Microsoft Windows Desktop Runtime 11.0.0 (x64)", "11.0.0.26425", "11.0.0", false)]
    [DataRow(InstallComponent.WindowsDesktop, "Microsoft Windows Desktop Runtime 11.0.0 (x64)", "11.0.0.50000", "11.0.0-rc.3.26522.148", true)]
    [DataRow(InstallComponent.WindowsDesktop, "Microsoft Windows Desktop Runtime 10.0.1 (x64)", "10.0.1.50000", "10.0.2", false)]
    public void InstalledPrerelease_BlocksOnlyEarlierBuilds(InstallComponent component, string displayName, string displayVersion, string requestedVersion, bool blocks)
    {
        var requested = new DotnetInstall(new DotnetInstallRoot(@"C:\Program Files\dotnet", InstallArchitecture.x64), new ReleaseVersion(requestedVersion), component);
        Register(displayName, FamilyCode(requested), displayVersion);

        var check = () => InstallerOrchestratorSingleton.ThrowIfNewerVersionInstalled(
            requested, DotnetEnvironmentManager.ReadInstalledBundles(_uninstallKey, requested));

        if (blocks)
        {
            check.Should().Throw<DotnetInstallException>().Which.ErrorCode.Should().Be(DotnetInstallErrorCode.NewerVersionInstalled);
        }
        else
        {
            check.Should().NotThrow();
        }
    }

    private static DotnetInstall MakeSdk(string version, InstallArchitecture architecture = InstallArchitecture.x64) =>
        new(new DotnetInstallRoot(@"C:\Program Files\dotnet", architecture), new ReleaseVersion(version), InstallComponent.SDK);

    private static Guid FamilyCode(DotnetInstall install) =>
        BundleUpgradeCodes.For(install.Component, install.Version, install.InstallRoot.Architecture)[0];

    private void RegisterBundle(string displayName, Guid upgradeCode) => Register(displayName, upgradeCode);

    private void Register(string displayName, Guid? upgradeCode, string? displayVersion = null)
    {
        using var entry = _uninstallKey.CreateSubKey(Guid.NewGuid().ToString("B"));
        entry.SetValue("DisplayName", displayName);
        if (displayVersion is not null)
        {
            entry.SetValue("DisplayVersion", displayVersion);
        }
        if (upgradeCode is { } code)
        {
            entry.SetValue("BundleUpgradeCode", new[] { code.ToString("B") }, RegistryValueKind.MultiString);
        }
    }
}
