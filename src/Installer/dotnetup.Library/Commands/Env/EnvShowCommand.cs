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

    public EnvShowCommand(ParseResult result, IDotnetEnvironmentManager? dotnetEnvironment = null, IEnvironmentStateInspector? inspector = null) : base(result)
    {
        _dotnetEnvironment = dotnetEnvironment ?? new DotnetEnvironmentManager();
        _inspector = inspector ?? new EnvironmentStateInspector(_dotnetEnvironment);
        _shellProvider = result.GetValue(CommonOptions.ShellOption);
    }

    protected override string GetCommandName() => "env show";

    protected override void ExecuteCore()
    {
        DotnetupConfigData? config = DotnetupConfig.Read();
        if (config is null)
        {
            Console.WriteLine("No dotnetup env configuration found. Run 'dotnetup env set <mode>' to configure.");
            return;
        }

        Console.WriteLine("dotnetup environment:");
        Console.WriteLine($"  {"dotnet access",-18}{config.AccessMode.ToString().ToLowerInvariant()}");
        Console.WriteLine($"  {"dotnetup on PATH",-18}{(config.DotnetupOnPath ? "true" : "false")}");

        IEnvShellProvider? shellProvider = _shellProvider ?? ShellDetection.GetCurrentShellProvider();
        ObservedEnvironmentState observed = _inspector.Inspect(shellProvider);
        var drift = EnvDriftAnalyzer.Compare(config, observed);

        if (drift.Count == 0)
        {
            Console.WriteLine();
            EnvTerminalState terminalState = EnvActivationStatus.EvaluateCurrentProcess(config, _dotnetEnvironment);
            if (terminalState.IsActive)
            {
                Console.WriteLine("In sync.");
                return;
            }

            Console.WriteLine("In sync, but not yet active in this terminal.");
            string? activationCommand = terminalState.NeedsRemovals
                ? null
                : EnvActivationCommandBuilder.TryBuild(shellProvider, config.AccessMode, config.DotnetupOnPath);

            if (activationCommand is not null)
            {
                Console.WriteLine("To apply it to this terminal now, run:");
                Console.WriteLine($"  {activationCommand}");
                Console.WriteLine("Or open a new terminal.");
            }
            else
            {
                Console.WriteLine("Open a new terminal to pick up the change.");
            }
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Detected drift between configured settings and the current environment:");
        foreach (var item in drift)
        {
            Console.WriteLine($"  - {item}");
        }
        Console.WriteLine();
        Console.WriteLine("Run 'dotnetup env set' to re-sync.");
    }
}
