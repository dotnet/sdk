// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Globalization;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal class EnvSetCommand : CommandBase
{
    private readonly DotnetAccessMode? _modeArg;
    private readonly bool? _dotnetupOnPathArg;
    private readonly IDotnetEnvironmentManager _dotnetEnvironment;
    private readonly IEnvironmentStateInspector _inspector;
    private readonly IEnvShellProvider? _shellProvider;

    public EnvSetCommand(ParseResult result, IDotnetEnvironmentManager? dotnetEnvironment = null, IEnvironmentStateInspector? inspector = null) : base(result, "env set")
    {
        _dotnetEnvironment = dotnetEnvironment ?? new DotnetEnvironmentManager();
        _inspector = inspector ?? new EnvironmentStateInspector(_dotnetEnvironment);
        _modeArg = result.GetValue(EnvSetCommandParser.ModeArgument);
        _dotnetupOnPathArg = result.GetValue(EnvSetCommandParser.DotnetupOnPathOption);
        _shellProvider = result.GetValue(CommonOptions.ShellOption);
    }

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

        EnvSettingsWriter.ApplyAndPersist(targetEnv, targetDotnetupOnPath, _dotnetEnvironment, _shellProvider, _inspector);
    }
}
