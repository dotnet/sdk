// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class AdminAccessModeTests
{
    [TestMethod]
    public void AdminMode_RequiresWindowsAndDoesNotReplaceOrMigrate()
    {
        DotnetAccessModePolicy.RequiresWindows(DotnetAccessMode.Admin).Should().BeTrue();
        DotnetAccessModePolicy.IsSupportedOnCurrentPlatform(DotnetAccessMode.Admin).Should().Be(OperatingSystem.IsWindows());
        DotnetAccessModePolicy.ShouldReplaceSystemConfiguration(DotnetAccessMode.Admin).Should().BeFalse();
        DotnetAccessModePolicy.ShouldMigrateSystemInstallsByDefault(DotnetAccessMode.Admin).Should().BeFalse();
    }

    [TestMethod, OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void EnvSettingsApplier_AdminOnNonWindows_ThrowsPlatformNotSupported()
    {
        Action act = () => EnvSettingsApplier.Apply(
            DotnetAccessMode.Admin,
            targetDotnetupOnPath: false,
            observed: ObservedEnvironmentState.Empty,
            environment: new MockDotnetInstallManager(Path.GetTempPath()),
            dotnetRoot: "dotnet-root");

        act.Should().Throw<PlatformNotSupportedException>();
    }

    [TestMethod]
    public void InitFormModel_DescribesAdmin()
    {
        InitFormModel.AccessModeDescription(DotnetAccessMode.Admin).Should().Contain("shared system folder");
    }

    [TestMethod]
    [DataRow(DotnetInstallErrorCode.AdminModeCommandNotSupported, ErrorCategory.User)]
    [DataRow(DotnetInstallErrorCode.AdminModeSymbolNotSupported, ErrorCategory.User)]
    [DataRow(DotnetInstallErrorCode.AdminModeRequiresUntracked, ErrorCategory.User)]
    [DataRow(DotnetInstallErrorCode.AdminModeChangeFailed, ErrorCategory.User)]
    [DataRow(DotnetInstallErrorCode.InstallerBusy, ErrorCategory.User)]
    [DataRow(DotnetInstallErrorCode.InstallerFailed, ErrorCategory.Product)]
    [DataRow(DotnetInstallErrorCode.InstallerPostInstallVerificationFailed, ErrorCategory.Product)]
    [DataRow(DotnetInstallErrorCode.WrongInstallerFileExtension, ErrorCategory.Product)]
    internal void AdminModeErrorCodes_AreClassified(DotnetInstallErrorCode errorCode, ErrorCategory expected)
    {
        ErrorCategoryClassifier.ClassifyInstallError(errorCode).Should().Be(expected);
    }
}
