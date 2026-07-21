// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal class EnvShowCommand : CommandBase
{
    private readonly IDotnetEnvironmentManager _dotnetEnvironment;
    private readonly IEnvironmentStateInspector _inspector;
    private readonly IEnvShellProvider? _shellProvider;

    public EnvShowCommand(ParseResult result, IDotnetEnvironmentManager? dotnetEnvironment = null, IEnvironmentStateInspector? inspector = null) : base(result, "env show")
    {
        _dotnetEnvironment = dotnetEnvironment ?? new DotnetEnvironmentManager();
        _inspector = inspector ?? new EnvironmentStateInspector(_dotnetEnvironment);
        _shellProvider = result.GetValue(CommonOptions.ShellOption);
    }

    protected override void ExecuteCore()
    {
        DotnetupConfigData? config = DotnetupConfig.Read();
        if (config is null)
        {
            Console.WriteLine(Strings.EnvShowNoConfiguration);
            return;
        }

        Console.WriteLine(Strings.EnvShowHeader);

        // Left-align the values in a single column whose width is derived from the widest
        // (localizable) label plus a small gap, so the labels stay aligned in any language.
        const int labelValueGap = 2;
        int labelWidth = Math.Max(Strings.EnvShowLabelDotnetAccess.Length, Strings.EnvShowLabelDotnetupOnPath.Length) + labelValueGap;
        Console.WriteLine($"  {Strings.EnvShowLabelDotnetAccess.PadRight(labelWidth)}{config.AccessMode.ToString().ToLowerInvariant()}");
        Console.WriteLine($"  {Strings.EnvShowLabelDotnetupOnPath.PadRight(labelWidth)}{(config.DotnetupOnPath ? "true" : "false")}");

        IEnvShellProvider? shellProvider = _shellProvider ?? ShellDetection.GetCurrentShellProvider();
        ObservedEnvironmentState observed = _inspector.Inspect(shellProvider);
        var drift = EnvDriftAnalyzer.Compare(config, observed);

        if (drift.Count == 0)
        {
            Console.WriteLine();
            EnvTerminalState terminalState = EnvActivationStatus.EvaluateCurrentProcess(config, _dotnetEnvironment);
            if (terminalState.IsActive)
            {
                Console.WriteLine(Strings.EnvShowInSync);
                return;
            }

            Console.WriteLine(Strings.EnvShowInSyncNotActive);
            string? activationCommand = terminalState.NeedsRemovals
                ? null
                : EnvActivationCommandBuilder.TryBuild(shellProvider, config.AccessMode, config.DotnetupOnPath);

            if (activationCommand is not null)
            {
                Console.WriteLine(Strings.EnvShowApplyToTerminalPrompt);
                Console.WriteLine($"  {activationCommand}");
                Console.WriteLine(Strings.EnvShowOrOpenNewTerminal);
            }
            else
            {
                Console.WriteLine(Strings.EnvShowOpenNewTerminalToPickUp);
            }
            return;
        }

        Console.WriteLine();
        Console.WriteLine(Strings.EnvShowDriftHeader);
        foreach (var item in drift)
        {
            Console.WriteLine($"  - {item}");
        }
        Console.WriteLine();
        Console.WriteLine(Strings.EnvShowRunSetToResync);
    }
}
