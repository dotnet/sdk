// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
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

    /// <remarks>
    /// The policy of <paramref name="adminModeSwitcher"/> is also the policy this command reads. Without a
    /// switcher, the command uses the machine policy.
    /// </remarks>
    public EnvSetCommand(
        ParseResult result,
        IDotnetEnvironmentManager? dotnetEnvironment = null,
        IEnvironmentStateInspector? inspector = null,
        AdminModeSwitcher? adminModeSwitcher = null) : base(result, "env set", adminModeSwitcher?.Policy)
    {
        _dotnetEnvironment = dotnetEnvironment ?? new DotnetEnvironmentManager();
        _inspector = inspector ?? new EnvironmentStateInspector(_dotnetEnvironment);
        AdminSwitcher = adminModeSwitcher;
        _modeArg = result.GetValue(EnvSetCommandParser.ModeArgument);
        _dotnetupOnPathArg = result.GetValue(EnvSetCommandParser.DotnetupOnPathOption);
        _shellProvider = result.GetValue(CommonOptions.ShellOption);
    }

    /// <inheritdoc/>
    internal override AdminModeSupport GetAdminModeSupport(Command command) => AdminModeSupport.AnySymbol;

    protected override void ExecuteCore()
    {
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

        if (targetEnv == DotnetAccessMode.Admin)
        {
            EnterAdminMode(previous);
            return;
        }

        // Leaving admin mode clears its machine-wide policy first (elevating if needed). Disable throws
        // if elevation is declined or the change fails, so no user-level settings are applied then.
        if (previous?.AccessMode == DotnetAccessMode.Admin)
        {
            AdminSwitcher.Disable();
        }

        EnvSettingsWriter.ApplyAndPersist(targetEnv, targetDotnetupOnPath, _dotnetEnvironment, _shellProvider, _inspector);
    }

    [AllowNull]
    private AdminModeSwitcher AdminSwitcher => field ?? new AdminModeSwitcher(Policy);

    /// <summary>
    /// Enters admin mode: sets the machine-wide policy (elevating if needed), then records admin as
    /// the user's access mode. The user's existing profiles, PATH entries, and DOTNET_ROOT are left
    /// unchanged. Selecting admin mode while it is already active changes nothing unless the policy
    /// has drifted.
    /// </summary>
    private void EnterAdminMode(DotnetupConfigData? previous)
    {
        if (previous?.AccessMode == DotnetAccessMode.Admin && AdminSwitcher.Policy.IsUnsignedDownloadBlocked())
        {
            SpectreAnsiConsole.MarkupLine(DotnetupTheme.Dim(Strings.AdminModeAlreadyEnabled));
            return;
        }

        AdminSwitcher.Enable();
        DotnetupConfig.WriteAccessSettings(DotnetAccessMode.Admin, previous?.DotnetupOnPath ?? true);
    }
}
