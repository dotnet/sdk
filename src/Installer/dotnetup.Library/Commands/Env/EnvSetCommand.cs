// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal class EnvSetCommand : CommandBase
{
    private readonly PathPreference _target;
    private readonly IDotnetEnvironmentManager _dotnetEnvironment;
    private readonly IEnvShellProvider? _shellProvider;

    public EnvSetCommand(ParseResult result, IDotnetEnvironmentManager? dotnetEnvironment = null) : base(result)
    {
        _dotnetEnvironment = dotnetEnvironment ?? new DotnetEnvironmentManager();
        _target = result.GetValue(EnvSetCommandParser.ModeArgument);
        _shellProvider = result.GetValue(CommonOptions.ShellOption);
    }

    protected override string GetCommandName() => "env set";

    protected override void ExecuteCore()
    {
        if (_target == PathPreference.All && !OperatingSystem.IsWindows())
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.PlatformNotSupported,
                "'env set all' is only supported on Windows. Use 'env set shell' on this platform.");
        }

        PathPreference? previous = DotnetupConfig.ReadPathPreference();
        string dotnetRoot = _dotnetEnvironment.GetDefaultDotnetInstallPath();

        PathPreferenceApplier.Apply(_target, previous, _dotnetEnvironment, dotnetRoot, _shellProvider);

        DotnetupConfig.Write(new DotnetupConfigData { PathPreference = _target });

        Console.WriteLine($"dotnetup env mode set to '{_target.ToString().ToLowerInvariant()}'.");
        Console.WriteLine("NOTE: You may need to restart your terminal for the changes to take effect.");
    }
}
