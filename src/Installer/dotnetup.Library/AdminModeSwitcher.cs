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
            throw new PlatformNotSupportedException(Strings.AdminModeWindowsOnly);
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
/// Applies the machine-wide side of entering or leaving admin mode: the <c>BlockUnsignedDownloads</c>
/// policy value. The access mode itself is stored in the per-user dotnetup config by the caller.
/// The policy change happens before any other state change, so a declined or failed elevation
/// leaves the machine and the user's configuration untouched.
/// </summary>
internal sealed class AdminModeSwitcher
{
    private readonly IAdminModeElevator _elevator;
    private readonly Func<bool> _isPrivilegedProcess;
    private readonly IAnsiConsole _console;

    /// <param name="policy">The machine policy to change; defaults to the machine policy for the current platform.</param>
    /// <param name="elevator">Runs the change elevated when this process is not privileged.</param>
    /// <param name="isPrivilegedProcess">Whether this process can write the policy directly.</param>
    /// <param name="console">Receives the notices printed around the change.</param>
    public AdminModeSwitcher(
        DotnetupPolicy? policy = null,
        IAdminModeElevator? elevator = null,
        Func<bool>? isPrivilegedProcess = null,
        IAnsiConsole? console = null)
    {
        Policy = policy ?? new DotnetupPolicy();
        _elevator = elevator ?? new WindowsAdminModeElevator();
        _isPrivilegedProcess = isPrivilegedProcess ?? (() => Environment.IsPrivilegedProcess);
        _console = console ?? SpectreAnsiConsole.Console;
    }

    /// <summary>The machine policy this switcher reads and changes.</summary>
    internal DotnetupPolicy Policy { get; }

    /// <summary>
    /// Enables admin mode's unsigned-download block, elevating only when the policy is not already
    /// set. Returns false when the policy was already set.
    /// </summary>
    public bool Enable() => Switch(AdminModeOperation.Enable);

    /// <summary>
    /// Clears admin mode's unsigned-download block, elevating only when the policy is set. Returns
    /// false when the policy was already clear.
    /// </summary>
    public bool Disable() => Switch(AdminModeOperation.Disable);

    /// <summary>
    /// Writes <paramref name="operation"/> to <paramref name="policy"/>. Requires administrator
    /// privileges for the machine policy; used directly when privileged and by the elevated child.
    /// </summary>
    internal static void ApplyPolicyChange(DotnetupPolicy policy, AdminModeOperation operation)
    {
        if (operation == AdminModeOperation.Enable)
        {
            policy.BlockUnsignedDownloads();
        }
        else
        {
            policy.AllowUnsignedDownloads();
        }
    }

    private bool Switch(AdminModeOperation operation)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.PlatformNotSupported,
                string.Format(CultureInfo.InvariantCulture, Strings.EnvModeWindowsOnly, AccessModeCliParsing.ToToken(DotnetAccessMode.Admin)));
        }

        bool enable = operation == AdminModeOperation.Enable;
        bool changed = Policy.IsUnsignedDownloadBlocked() != enable;
        if (changed)
        {
            bool privileged = _isPrivilegedProcess();
            WriteMachineWideNotice(enable, privileged);
            ApplyChange(operation, privileged);
            VerifyChange(enable);
        }

        if (enable)
        {
            _console.MarkupLine(DotnetupTheme.Success(Strings.AdminModeEnabled));
            _console.WriteLine(Strings.AdminModePreservesUserEnvironment);
        }
        else
        {
            _console.MarkupLine(DotnetupTheme.Success(Strings.AdminModeDisabled));
            if (changed)
            {
                _console.WriteLine(Strings.AdminModeUnsignedDownloadsAllowed);
            }
        }

        return changed;
    }

    private void WriteMachineWideNotice(bool enable, bool privileged)
    {
        string notice = enable ? Strings.AdminModeEnablingNotice : Strings.AdminModeDisablingNotice;
        if (!privileged)
        {
            notice += " " + Strings.AdminModeElevationNotice;
        }

        _console.MarkupLine(DotnetupTheme.Notice(notice.EscapeMarkup()));
    }

    private void ApplyChange(AdminModeOperation operation, bool privileged)
    {
        try
        {
            if (privileged)
            {
                ApplyPolicyChange(Policy, operation);
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
                Strings.AdminModeChangeCancelled,
                ex);
        }
        catch (Exception ex) when (IsChangeFailure(ex))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.AdminModeChangeFailed,
                string.Format(
                    CultureInfo.CurrentCulture,
                    operation == AdminModeOperation.Enable ? Strings.AdminModeEnableFailed : Strings.AdminModeDisableFailed,
                    ex.Message),
                ex);
        }
    }

    // Re-read the policy: Group Policy or a restrictive ACL can leave the value unchanged even
    // though the elevated write reported success.
    private void VerifyChange(bool enable)
    {
        if (Policy.IsUnsignedDownloadBlocked() != enable)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.AdminModeChangeFailed,
                enable ? Strings.AdminModeNotEnabled : Strings.AdminModeNotDisabled);
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

}
