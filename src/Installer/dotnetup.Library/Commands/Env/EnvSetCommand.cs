// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Globalization;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal class EnvSetCommand : CommandBase
{
    private readonly DotnetAccessMode? _modeArg;
    private readonly bool? _dotnetupOnPathArg;
    private readonly IDotnetEnvironmentManager _dotnetEnvironment;
    private readonly IEnvironmentStateInspector _inspector;
    private readonly IEnvShellProvider? _shellProvider;
    private readonly AdminModeSwitcher? _adminModeSwitcher;

    public EnvSetCommand(
        ParseResult result,
        IDotnetEnvironmentManager? dotnetEnvironment = null,
        IEnvironmentStateInspector? inspector = null,
        AdminModeSwitcher? adminModeSwitcher = null) : base(result, "env set")
    {
        _dotnetEnvironment = dotnetEnvironment ?? new DotnetEnvironmentManager();
        _inspector = inspector ?? new EnvironmentStateInspector(_dotnetEnvironment);
        _adminModeSwitcher = adminModeSwitcher;
        _modeArg = result.GetValue(EnvSetCommandParser.ModeArgument);
        _dotnetupOnPathArg = result.GetValue(EnvSetCommandParser.DotnetupOnPathOption);
        _shellProvider = result.GetValue(CommonOptions.ShellOption);
    }

    /// <inheritdoc/>
    internal override AdminModeSupport GetAdminModeSupport(Command command) => AdminModeSupport.AnySymbol;

    protected override void ExecuteCore()
    {
        if (HandleAdminMode())
        {
            return;
        }
        DotnetupConfigData? previous = DotnetupConfig.Read();

        // Resolve the dotnet-access mode: explicit argument wins, otherwise re-apply the
        // stored mode. With neither, there is nothing to apply — distinguish "no config" from
        // "config present but unreadable" so the error tells the user what is actually wrong.
        DotnetAccessMode targetEnv;
        if (_modeArg is DotnetAccessMode mode)
        {
            targetEnv = mode;
        }
        else if (previous?.AccessMode is DotnetAccessMode storedEnv)
        {
            targetEnv = storedEnv;
        }
        else if (DotnetupConfig.Exists())
        {
            // A config file exists but could not be read (Read already warned about the corruption).
            throw new DotnetInstallException(
                DotnetInstallErrorCode.UserConfigurationCorrupted,
                Strings.EnvSetConfigUnreadable);
        }
        else
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.InvalidArguments,
                Strings.EnvSetNoStoredConfig);
        }

        if (!DotnetAccessModePolicy.IsSupportedOnCurrentPlatform(targetEnv))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.PlatformNotSupported,
                string.Format(CultureInfo.InvariantCulture, Strings.EnvModeWindowsOnly, targetEnv.ToString().ToLowerInvariant()));
        }

        // dotnetup-on-PATH: explicit flag wins, otherwise keep the stored value, otherwise default on.
        bool targetDotnetupOnPath = _dotnetupOnPathArg ?? previous?.DotnetupOnPath ?? true;

        // HandleAdminMode already handled 'env set admin' and the no-mode case, so if admin mode is
        // still active here, the user explicitly requested a non-admin mode. Disable admin mode first
        // (elevating if needed) before writing any user-level settings. Disable throws if elevation is
        // declined or the change fails, so nothing below is applied in that case.
        if (AdminModeSwitcher.IsAdminModeActive())
        {
            AdminSwitcher.Disable();
        }

        EnvSettingsWriter.ApplyAndPersist(targetEnv, targetDotnetupOnPath, _dotnetEnvironment, _shellProvider, _inspector);
    }

    private AdminModeSwitcher AdminSwitcher => _adminModeSwitcher ?? new AdminModeSwitcher();

    /// <summary>
    /// Routes admin mode before anything else changes: <c>env set admin</c> only enables admin
    /// mode, and no mode while in admin mode changes nothing. Another explicit mode leaves admin
    /// mode right before it is applied. Returns true when the command is fully handled.
    /// </summary>
    private bool HandleAdminMode()
    {
        if (_modeArg == DotnetAccessMode.Admin)
        {
            AdminSwitcher.Enable();
            return true;
        }

        if (_modeArg is null && AdminModeSwitcher.IsAdminModeActive())
        {
            AdminModeMessages.WriteAdminModeActive(SpectreAnsiConsole.Console);
            return true;
        }

        return false;
    }
}
