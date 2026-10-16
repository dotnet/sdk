// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Globalization;
using System.Security;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.ElevatedSystemPath;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>The machine-wide admin mode change to perform.</summary>
internal enum AdminModeOperation
{
    Enable,
    Disable,
}

/// <summary>Performs an admin mode policy change with administrator privileges.</summary>
internal interface IAdminModeElevator
{
    /// <summary>
    /// Runs <paramref name="operation"/> elevated and waits for it to finish. Throws when the user
    /// declines elevation or the elevated operation fails.
    /// </summary>
    void Run(AdminModeOperation operation);
}

/// <summary>
/// Runs the admin mode change in a one-shot elevated <c>dotnetup elevatedsystempath</c> child
/// process, which triggers a single UAC prompt. Only fixed operation tokens are passed on the
/// command line; the policy location and values are owned by <see cref="DotnetupPolicy"/>.
/// </summary>
internal sealed class WindowsAdminModeElevator : IAdminModeElevator
{
    public void Run(AdminModeOperation operation)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Admin mode is only supported on Windows.");
        }

        WindowsPathHelper.StartElevatedProcess(ToOperationToken(operation));
    }

    internal static string ToOperationToken(AdminModeOperation operation) => operation switch
    {
        AdminModeOperation.Enable => ElevatedSystemPathCommandParser.EnableAdminModeOperation,
        AdminModeOperation.Disable => ElevatedSystemPathCommandParser.DisableAdminModeOperation,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
    };
}

/// <summary>
/// Switches the machine into or out of admin mode by setting the <c>AdminMode</c> policy value.
/// The policy change happens before any other state change, so a declined or failed elevation
/// leaves the machine and the user's configuration untouched.
/// </summary>
internal sealed class AdminModeSwitcher
{
    private readonly IAdminModeElevator _elevator;
    private readonly Func<bool> _isPrivilegedProcess;
    private readonly IAnsiConsole _console;

    public AdminModeSwitcher(
        IAdminModeElevator? elevator = null,
        Func<bool>? isPrivilegedProcess = null,
        IAnsiConsole? console = null)
    {
        _elevator = elevator ?? new WindowsAdminModeElevator();
        _isPrivilegedProcess = isPrivilegedProcess ?? (() => Environment.IsPrivilegedProcess);
        _console = console ?? SpectreAnsiConsole.Console;
    }

    /// <summary>True when the effective access mode is admin mode.</summary>
    public static bool IsAdminModeActive() => AccessModeResolver.Resolve().IsAdmin;

    /// <summary>Enables admin mode. Returns false when it was already enabled.</summary>
    public bool Enable() => Switch(AdminModeOperation.Enable);

    /// <summary>Disables admin mode. Returns false when it was already disabled.</summary>
    public bool Disable() => Switch(AdminModeOperation.Disable);

    private bool Switch(AdminModeOperation operation)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.PlatformNotSupported,
                string.Format(CultureInfo.InvariantCulture, Strings.EnvModeWindowsOnly, AccessModeCliParsing.ToToken(DotnetAccessMode.Admin)));
        }

        bool enable = operation == AdminModeOperation.Enable;
        if (DotnetupPolicy.IsAdminModeEnabled() == enable)
        {
            _console.MarkupLine(DotnetupTheme.Dim($"Admin mode is already {StateWord(enable)}."));
            return false;
        }

        bool privileged = _isPrivilegedProcess();
        WriteMachineWideNotice(enable, privileged);
        ApplyChange(operation, privileged);
        VerifyChange(enable);

        _console.MarkupLine(DotnetupTheme.Success($"Admin mode is {StateWord(enable)}."));
        if (enable)
        {
            _console.MarkupLine(DotnetupTheme.Notice(Strings.AdminModePreservesUserEnvironment.EscapeMarkup()));
        }

        return true;
    }

    private void WriteMachineWideNotice(bool enable, bool privileged)
    {
        string notice = $"{(enable ? "Enabling" : "Disabling")} admin mode changes a machine-wide policy that affects all users on this computer.";
        if (!privileged)
        {
            notice += " Windows will ask for administrator permission.";
        }

        _console.MarkupLine(DotnetupTheme.Notice(notice.EscapeMarkup()));
    }

    private void ApplyChange(AdminModeOperation operation, bool privileged)
    {
        try
        {
            if (privileged)
            {
                WritePolicy(operation);
            }
            else
            {
                _elevator.Run(operation);
            }
        }
        catch (DotnetInstallException ex) when (ex.ErrorCode == DotnetInstallErrorCode.PermissionDenied)
        {
            // The elevation helper reports a declined UAC prompt as PermissionDenied.
            throw new DotnetInstallException(
                DotnetInstallErrorCode.AdminModeChangeFailed,
                "Elevation was cancelled. Admin mode was not changed.",
                ex);
        }
        catch (Exception ex) when (IsChangeFailure(ex))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.AdminModeChangeFailed,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Failed to {0} admin mode: {1}",
                    operation == AdminModeOperation.Enable ? "enable" : "disable",
                    ex.Message),
                ex);
        }
    }

    private static void WritePolicy(AdminModeOperation operation)
    {
        if (operation == AdminModeOperation.Enable)
        {
            DotnetupPolicy.EnableAdminMode();
        }
        else
        {
            DotnetupPolicy.DisableAdminMode();
        }
    }

    // Re-read the policy: Group Policy or a restrictive ACL can leave the value unchanged even
    // though the elevated write reported success.
    private static void VerifyChange(bool enable)
    {
        if (DotnetupPolicy.IsAdminModeEnabled() != enable)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.AdminModeChangeFailed,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Admin mode was not {0}. The machine policy did not change, which can happen when it is managed by Group Policy.",
                    StateWord(enable)));
        }
    }

    private static bool IsChangeFailure(Exception ex) =>
        ex is DotnetInstallException
            or InvalidOperationException
            or Win32Exception
            or UnauthorizedAccessException
            or IOException
            or SecurityException
            or PlatformNotSupportedException;

    private static string StateWord(bool enabled) => enabled ? "enabled" : "disabled";
}

/// <summary>Guidance printed around admin mode switches.</summary>
internal static class AdminModeMessages
{
    /// <summary>Printed when a command that would change the access mode runs in admin mode without a mode.</summary>
    public static void WriteAdminModeActive(IAnsiConsole console)
    {
        console.MarkupLine(DotnetupTheme.Notice("Admin mode is active on this machine, so no changes were made."));
        console.MarkupLine(DotnetupTheme.Dim(
            "To leave admin mode, run 'dotnetup init --mode <mode>' with 'none', 'shell', or 'everywhere'.".EscapeMarkup()));
    }

    /// <summary>Printed after switching into admin mode through <c>init</c>.</summary>
    public static void WriteNextSteps(IAnsiConsole console)
    {
        console.MarkupLine(DotnetupTheme.Dim(
            "Next, install the .NET SDK with 'dotnetup sdk install <version> --untracked'.".EscapeMarkup()));
    }
}
