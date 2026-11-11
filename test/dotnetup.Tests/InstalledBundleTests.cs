// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.Win32;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
[OSCondition(OperatingSystems.Windows)]
public class RegistryInstallStateTests
{
    private string _keyPath = null!;
    private RegistryKey _installedVersionsKey = null!;

    [TestInitialize]
    public void CreateKey()
    {
        _keyPath = $@"Software\dotnetup-tests\{Guid.NewGuid():N}";
        _installedVersionsKey = Registry.CurrentUser.CreateSubKey(_keyPath);
    }

    [TestCleanup]
    public void DeleteKey()
    {
        _installedVersionsKey.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
    }

    [TestMethod]
    public void ReadRegistryInstallState_ReadsSdkAndRuntimeRegistrationsForTheRequestedArchitecture()
    {
        Register(@"x64", "InstallLocation", @"C:\Program Files\dotnet\");
        Register(@"x64\sdk", "10.0.114");
        Register(@"x64\sdk", "11.0.100-rc.3.26522.148");
        Register(@"x64\sdk", "not-a-version");
        Register(@"x64\sharedfx\Microsoft.NETCore.App", "10.0.5");
        Register(@"x64\sharedfx\Microsoft.AspNetCore.App", "10.0.5");
        Register(@"x64\sharedfx\Microsoft.WindowsDesktop.App", "10.0.5");
        Register(@"x86\sdk", "10.0.199");

        var installs = DotnetEnvironmentManager.ReadRegistryInstallState(_installedVersionsKey, InstallArchitecture.x64);

        installs.Select(i => (i.Component, i.Version.ToString())).Should().BeEquivalentTo(new[]
        {
            (InstallComponent.SDK, "10.0.114"),
            (InstallComponent.SDK, "11.0.100-rc.3.26522.148"),
            (InstallComponent.Runtime, "10.0.5"),
            (InstallComponent.ASPNETCore, "10.0.5"),
            (InstallComponent.WindowsDesktop, "10.0.5"),
        });
        installs.Should().AllSatisfy(i => i.InstallRoot.Should().Be(new DotnetInstallRoot(@"C:\Program Files\dotnet", InstallArchitecture.x64)));
    }

    [TestMethod]
    public void ReadRegistryInstallState_ReturnsEmptyWhenArchitectureIsNotRegistered()
    {
        Register(@"x64\sdk", "10.0.114");

        DotnetEnvironmentManager.ReadRegistryInstallState(_installedVersionsKey, InstallArchitecture.arm64).Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(InstallComponent.SDK, InstallArchitecture.x64, @"x64\sdk")]
    [DataRow(InstallComponent.ASPNETCore, InstallArchitecture.arm64, @"arm64\sharedfx\Microsoft.AspNetCore.App")]
    public void GetRegistryVersionsKeyName_NamesTheKeyInRegistryEditorForm(InstallComponent component, InstallArchitecture architecture, string expectedSuffix)
    {
        string wowNode = Environment.Is64BitOperatingSystem ? @"WOW6432Node\" : string.Empty;

        DotnetEnvironmentManager.GetRegistryVersionsKeyName(component, architecture)
            .Should().Be($@"HKEY_LOCAL_MACHINE\SOFTWARE\{wowNode}dotnet\Setup\InstalledVersions\{expectedSuffix}");
    }

    [TestMethod]
    public void RegistrationWithMissingPayloadFiles_RejectsBundleDowngrade()
    {
        string emptyInstallLocation = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Register(@"x64", "InstallLocation", emptyInstallLocation);
            Register(@"x64\sdk", "10.0.114");
            var requested = new DotnetInstall(new DotnetInstallRoot(emptyInstallLocation, InstallArchitecture.x64), new ReleaseVersion("10.0.100"), InstallComponent.SDK);

            var registered = DotnetEnvironmentManager.ReadRegistryInstallState(_installedVersionsKey, InstallArchitecture.x64);
            var check = () => InstallerOrchestratorSingleton.ThrowIfNewerVersionInstalled(requested, registered);

            Directory.Exists(Path.Combine(emptyInstallLocation, "sdk", "10.0.114")).Should().BeFalse();
            check.Should().Throw<DotnetInstallException>()
                .Which.ErrorCode.Should().Be(DotnetInstallErrorCode.NewerVersionInstalled);
        }
        finally
        {
            Directory.Delete(emptyInstallLocation, recursive: true);
        }
    }

    private void Register(string subKeyPath, string valueName, object? value = null)
    {
        using var key = _installedVersionsKey.CreateSubKey(subKeyPath);
        if (value is null)
        {
            key.SetValue(valueName, 1, RegistryValueKind.DWord);
        }
        else
        {
            key.SetValue(valueName, value);
        }
    }
}
