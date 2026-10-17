// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
[DoNotParallelize]
public class AdminModeSwitcherTests
{
    private AdminModeTestEnvironment _environment = null!;
    private FakeDotnetupPolicyStore _store => _environment.Store;
    private StringWriter _output = null!;

    [TestInitialize]
    public void Initialize()
    {
        _environment = new AdminModeTestEnvironment();
        _output = new StringWriter();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _environment.Dispose();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Enable_WhenDisabled_RunsElevatorOnceAndEnables()
    {
        var elevator = _environment.CreateElevator();

        bool changed = CreateSwitcher(elevator).Enable();

        changed.Should().BeTrue();
        elevator.Operations.Should().Equal(AdminModeOperation.Enable);
        _environment.Policy.IsAdminModeEnabled().Should().BeTrue();
        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName].Should().BeTrue();
        _output.ToString().Should().Contain("affects all users").And.Contain("Admin mode is enabled.")
            .And.Contain("left unchanged").And.Contain("does not activate user-level .NET").And.Contain("restart open terminals");
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Enable_WhenAlreadyEnabled_DoesNotElevate()
    {
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;
        var elevator = _environment.CreateElevator();

        bool changed = CreateSwitcher(elevator).Enable();

        changed.Should().BeFalse();
        elevator.Operations.Should().BeEmpty();
        _store.Writes.Should().BeEmpty();
        _output.ToString().Should().Contain("already enabled");
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Disable_WhenEnabled_RunsElevatorOnceAndDisables()
    {
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;
        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = true;
        var elevator = _environment.CreateElevator();

        bool changed = CreateSwitcher(elevator).Disable();

        changed.Should().BeTrue();
        elevator.Operations.Should().Equal(AdminModeOperation.Disable);
        _environment.Policy.IsAdminModeEnabled().Should().BeFalse();
        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName].Should().BeTrue();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Disable_WhenAlreadyDisabled_DoesNotElevate()
    {
        var elevator = _environment.CreateElevator();

        CreateSwitcher(elevator).Disable().Should().BeFalse();

        elevator.Operations.Should().BeEmpty();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Enable_WhenPrivileged_WritesPolicyWithoutElevator()
    {
        var elevator = _environment.CreateElevator();

        bool changed = CreateSwitcher(elevator, privileged: true).Enable();

        changed.Should().BeTrue();
        elevator.Operations.Should().BeEmpty();
        _store.Writes.Should().ContainSingle();
        _environment.Policy.IsAdminModeEnabled().Should().BeTrue();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Enable_WhenPrivilegedWriteIsDenied_FailsWithAdminModeChangeFailed()
    {
        var store = new FakeDotnetupPolicyStore { WriteFailure = new UnauthorizedAccessException("Access to the registry key is denied.") };
        var elevator = _environment.CreateElevator();
        var switcher = new AdminModeSwitcher(new DotnetupPolicy(store), elevator, () => true, CreateConsole(_output));

        Action act = () => switcher.Enable();

        act.Should().Throw<DotnetInstallException>()
            .Where(ex => ex.ErrorCode == DotnetInstallErrorCode.AdminModeChangeFailed)
            .WithMessage("*registry key is denied*");
        elevator.Operations.Should().BeEmpty();
        store.Values.Should().BeEmpty();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Enable_WhenElevationCancelled_FailsAndLeavesPolicyUnchanged()
    {
        var elevator = _environment.CreateElevator(new DotnetInstallException(DotnetInstallErrorCode.PermissionDenied, "cancelled"));

        Action act = () => CreateSwitcher(elevator).Enable();

        act.Should().Throw<DotnetInstallException>()
            .Where(ex => ex.ErrorCode == DotnetInstallErrorCode.AdminModeChangeFailed)
            .WithMessage("Elevation was cancelled*");
        _store.Writes.Should().BeEmpty();
        _environment.Policy.IsAdminModeEnabled().Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Enable_WhenElevatedProcessFails_FailsWithAdminModeChangeFailed()
    {
        var elevator = _environment.CreateElevator(new InvalidOperationException("Elevated process returned exit code 1"));

        Action act = () => CreateSwitcher(elevator).Enable();

        act.Should().Throw<DotnetInstallException>()
            .Where(ex => ex.ErrorCode == DotnetInstallErrorCode.AdminModeChangeFailed)
            .WithMessage("*exit code 1*");
        _environment.Policy.IsAdminModeEnabled().Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Enable_WhenPolicyUnchangedAfterElevation_FailsWithAdminModeChangeFailed()
    {
        var elevator = _environment.CreateElevator(appliesChange: false);

        Action act = () => CreateSwitcher(elevator).Enable();

        act.Should().Throw<DotnetInstallException>()
            .Where(ex => ex.ErrorCode == DotnetInstallErrorCode.AdminModeChangeFailed);
        elevator.Operations.Should().Equal(AdminModeOperation.Enable);
        _output.ToString().Should().NotContain("Admin mode is enabled.");
    }

    [TestMethod, OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Enable_OffWindows_ThrowsPlatformNotSupported()
    {
        var elevator = _environment.CreateElevator();

        Action act = () => CreateSwitcher(elevator).Enable();

        act.Should().Throw<DotnetInstallException>()
            .Where(ex => ex.ErrorCode == DotnetInstallErrorCode.PlatformNotSupported);
        elevator.Operations.Should().BeEmpty();
    }

    [TestMethod]
    public void WindowsAdminModeElevator_UsesFixedOperationTokens()
    {
        WindowsAdminModeElevator.ToOperationToken(AdminModeOperation.Enable).Should().Be("enableadminmode");
        WindowsAdminModeElevator.ToOperationToken(AdminModeOperation.Disable).Should().Be("disableadminmode");
    }

    private AdminModeSwitcher CreateSwitcher(IAdminModeElevator elevator, bool privileged = false)
        => _environment.CreateSwitcher(elevator, privileged, _output);

    internal static IAnsiConsole CreateConsole(StringWriter output) => AnsiConsole.Create(new AnsiConsoleSettings
    {
        Out = new AnsiConsoleOutput(output),
        Ansi = AnsiSupport.No,
    });
}
