// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
[DoNotParallelize]
public class AdminModeSwitcherTests : IDisposable
{
    private readonly AdminModeTestEnvironment _environment = new();
    private readonly StringWriter _output = new();

    public void Dispose()
    {
        _output.Dispose();
        _environment.Dispose();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Enable_BlocksUnsignedDownloads()
    {
        var elevator = _environment.CreateElevator();

        _environment.CreateSwitcher(elevator, output: _output).Enable().Should().BeTrue();

        elevator.Operations.Should().Equal(AdminModeOperation.Enable);
        _environment.Policy.IsUnsignedDownloadBlocked().Should().BeTrue();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Disable_AllowsUnsignedDownloads()
    {
        _environment.Store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = true;
        var elevator = _environment.CreateElevator();

        _environment.CreateSwitcher(elevator, output: _output).Disable().Should().BeTrue();

        elevator.Operations.Should().Equal(AdminModeOperation.Disable);
        _environment.Policy.IsUnsignedDownloadBlocked().Should().BeFalse();
        _output.ToString().Should().Contain("allowed again by default");
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Enable_WhenElevationCancelled_LeavesPolicyUnchanged()
    {
        var elevator = _environment.CreateElevator(
            new DotnetInstallException(DotnetInstallErrorCode.PermissionDenied, "cancelled"));

        Action act = () => _environment.CreateSwitcher(elevator, output: _output).Enable();

        act.Should().Throw<DotnetInstallException>()
            .Where(ex => ex.ErrorCode == DotnetInstallErrorCode.AdminModeChangeFailed);
        _environment.Policy.IsUnsignedDownloadBlocked().Should().BeFalse();
    }

    internal static IAnsiConsole CreateConsole(StringWriter output) => AnsiConsole.Create(new AnsiConsoleSettings
    {
        Out = new AnsiConsoleOutput(output),
        Ansi = AnsiSupport.No,
    });
}
