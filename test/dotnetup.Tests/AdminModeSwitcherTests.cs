// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
[DoNotParallelize]
public class AdminModeSwitcherTests
{
    private FakeDotnetupPolicyStore _store = null!;
    private StringWriter _output = null!;

    [TestInitialize]
    public void Initialize()
    {
        _store = new FakeDotnetupPolicyStore();
        DotnetupPolicy.Store = _store;
        AccessModeResolver.OverrideForTesting = null;
        _output = new StringWriter();
    }

    [TestCleanup]
    public void Cleanup()
    {
        DotnetupPolicy.ResetStoreForTesting();
        AccessModeResolver.OverrideForTesting = null;
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Enable_WhenDisabled_RunsElevatorOnceAndEnables()
    {
        var elevator = new FakeAdminModeElevator();

        bool changed = CreateSwitcher(elevator).Enable();

        changed.Should().BeTrue();
        elevator.Operations.Should().Equal(AdminModeOperation.Enable);
        DotnetupPolicy.IsAdminModeEnabled().Should().BeTrue();
        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName].Should().BeTrue();
        _output.ToString().Should().Contain("affects all users").And.Contain("Admin mode is enabled.");
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Enable_WhenAlreadyEnabled_DoesNotElevate()
    {
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;
        var elevator = new FakeAdminModeElevator();

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
        var elevator = new FakeAdminModeElevator();

        bool changed = CreateSwitcher(elevator).Disable();

        changed.Should().BeTrue();
        elevator.Operations.Should().Equal(AdminModeOperation.Disable);
        DotnetupPolicy.IsAdminModeEnabled().Should().BeFalse();
        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName].Should().BeTrue();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Disable_WhenAlreadyDisabled_DoesNotElevate()
    {
        var elevator = new FakeAdminModeElevator();

        CreateSwitcher(elevator).Disable().Should().BeFalse();

        elevator.Operations.Should().BeEmpty();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Enable_WhenPrivileged_WritesPolicyWithoutElevator()
    {
        var elevator = new FakeAdminModeElevator();

        bool changed = CreateSwitcher(elevator, privileged: true).Enable();

        changed.Should().BeTrue();
        elevator.Operations.Should().BeEmpty();
        _store.Writes.Should().ContainSingle();
        DotnetupPolicy.IsAdminModeEnabled().Should().BeTrue();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Enable_WhenElevationCancelled_FailsAndLeavesPolicyUnchanged()
    {
        var elevator = new FakeAdminModeElevator()
        {
            Failure = new DotnetInstallException(DotnetInstallErrorCode.PermissionDenied, "cancelled"),
        };

        Action act = () => CreateSwitcher(elevator).Enable();

        act.Should().Throw<DotnetInstallException>()
            .Where(ex => ex.ErrorCode == DotnetInstallErrorCode.AdminModeChangeFailed)
            .WithMessage("Elevation was cancelled*");
        _store.Writes.Should().BeEmpty();
        DotnetupPolicy.IsAdminModeEnabled().Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Enable_WhenElevatedProcessFails_FailsWithAdminModeChangeFailed()
    {
        var elevator = new FakeAdminModeElevator()
        {
            Failure = new InvalidOperationException("Elevated process returned exit code 1"),
        };

        Action act = () => CreateSwitcher(elevator).Enable();

        act.Should().Throw<DotnetInstallException>()
            .Where(ex => ex.ErrorCode == DotnetInstallErrorCode.AdminModeChangeFailed)
            .WithMessage("*exit code 1*");
        DotnetupPolicy.IsAdminModeEnabled().Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Enable_WhenPolicyUnchangedAfterElevation_FailsWithAdminModeChangeFailed()
    {
        var elevator = new FakeAdminModeElevator() { AppliesChange = false };

        Action act = () => CreateSwitcher(elevator).Enable();

        act.Should().Throw<DotnetInstallException>()
            .Where(ex => ex.ErrorCode == DotnetInstallErrorCode.AdminModeChangeFailed);
        elevator.Operations.Should().Equal(AdminModeOperation.Enable);
        _output.ToString().Should().NotContain("Admin mode is enabled.");
    }

    [TestMethod, OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Enable_OffWindows_ThrowsPlatformNotSupported()
    {
        var elevator = new FakeAdminModeElevator();

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
        => new(elevator, () => privileged, CreateConsole(_output));

    internal static IAnsiConsole CreateConsole(StringWriter output) => AnsiConsole.Create(new AnsiConsoleSettings
    {
        Out = new AnsiConsoleOutput(output),
        Ansi = AnsiSupport.No,
    });
}

/// <summary>In-memory <see cref="IDotnetupPolicyStore"/> so tests never touch HKLM.</summary>
internal sealed class FakeDotnetupPolicyStore : IDotnetupPolicyStore
{
    public Dictionary<string, bool> Values { get; } = new(StringComparer.Ordinal);

    public List<Dictionary<string, bool>> Writes { get; } = [];

    public bool ReadFlag(string valueName) => Values.TryGetValue(valueName, out bool value) && value;

    public void WriteFlags(IReadOnlyDictionary<string, bool> values)
    {
        Writes.Add(new Dictionary<string, bool>(values));
        foreach (var (name, value) in values)
        {
            Values[name] = value;
        }
    }
}

/// <summary>
/// Simulates the elevated child process: records the operation and, unless told otherwise,
/// applies it through <see cref="DotnetupPolicy"/> (backed by the fake store). Never prompts UAC.
/// </summary>
internal sealed class FakeAdminModeElevator : IAdminModeElevator
{
    public List<AdminModeOperation> Operations { get; } = [];

    public Exception? Failure { get; init; }

    public bool AppliesChange { get; init; } = true;

    public void Run(AdminModeOperation operation)
    {
        Operations.Add(operation);
        if (Failure is not null)
        {
            throw Failure;
        }

        if (!AppliesChange)
        {
            return;
        }

        if (operation == AdminModeOperation.Enable)
        {
            DotnetupPolicy.EnableAdminMode();
        }
        else
        {
            DotnetupPolicy.DisableAdminMode();
        }
    }
}
