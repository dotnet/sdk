// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal class EnvSetCommand : CommandBase
{
    private readonly PathPreference? _modeArg;
    private readonly bool? _dotnetupOnPathArg;
    private readonly IDotnetEnvironmentManager _dotnetEnvironment;
    private readonly IEnvShellProvider? _shellProvider;

    public EnvSetCommand(ParseResult result, IDotnetEnvironmentManager? dotnetEnvironment = null) : base(result)
    {
        _dotnetEnvironment = dotnetEnvironment ?? new DotnetEnvironmentManager();
        _modeArg = result.GetValue(EnvSetCommandParser.ModeArgument);
        _dotnetupOnPathArg = EnvSetCommandParser.ParseDotnetupOnPath(result.GetValue(EnvSetCommandParser.DotnetupOnPathOption));
        _shellProvider = result.GetValue(CommonOptions.ShellOption);
    }

    protected override string GetCommandName() => "env set";

    protected override void ExecuteCore()
    {
        DotnetupConfigData? previous = DotnetupConfig.Read();

        // Resolve the dotnet-exposure mode: explicit argument wins, otherwise re-apply the
        // stored mode. With neither, there is nothing to apply.
        PathPreference targetEnv;
        if (_modeArg is PathPreference mode)
        {
            targetEnv = mode;
        }
        else if (previous?.Env is PathPreference storedEnv)
        {
            targetEnv = storedEnv;
        }
        else
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.Unknown,
                "No env mode specified and none is stored. Specify a mode: none, shell, or all.");
        }

        if (targetEnv == PathPreference.All && !OperatingSystem.IsWindows())
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.PlatformNotSupported,
                "'all' mode is only supported on Windows. Use 'shell' on this platform.");
        }

        // dotnetup-on-PATH: explicit flag wins, otherwise keep the stored value, otherwise default on.
        bool targetDotnetupOnPath = _dotnetupOnPathArg ?? previous?.DotnetupOnPath ?? true;

        EnvSettingsWriter.ApplyAndPersist(targetEnv, targetDotnetupOnPath, _dotnetEnvironment, _shellProvider);
    }
}
