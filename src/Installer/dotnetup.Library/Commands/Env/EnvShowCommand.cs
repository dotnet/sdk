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
        Console.WriteLine($"  dotnet access    {config.AccessMode.ToString().ToLowerInvariant()}");
        Console.WriteLine($"  dotnetup on PATH   {(config.DotnetupOnPath ? "true" : "false")}");

        IEnvShellProvider? shellProvider = _shellProvider ?? ShellDetection.GetCurrentShellProvider();
        ObservedEnvironmentState observed = _inspector.Inspect(shellProvider);
        var drift = EnvDriftAnalyzer.Compare(config, observed);

        if (drift.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("In sync.");
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
