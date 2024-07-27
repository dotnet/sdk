// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Completions;

using System.CommandLine;
using System.Linq;

public class CompletionsCommand : CliCommand
{
    private static Shells.IShellProvider[] _knownShells = [
        new Shells.BashShellProvider(),
        new Shells.PowershellShellProvider(),
        new Shells.FishShellProvider(),
        new Shells.ZshShellProvider(),
        new Shells.NushellShellProvider()
    ];

    private static Dictionary<string, Shells.IShellProvider> ShellProvidersByArgument = _knownShells.ToDictionary(p => p.ArgumentName);

    public CompletionsCommand()
        : base("completions", "Commands for generating and registering completions for supported shells")
    {
        var shellArg = new CliArgument<Shells.IShellProvider>("shell")
        {
            Description = "The shell for which to generate or register completions",
            Arity = ArgumentArity.ZeroOrOne,
            // called when no token is presented at all
            DefaultValueFactory = (argResult) => LookupShellFromEnvironment(),
            // called for all other scenarios
            CustomParser = (argResult) =>
            {
                return argResult.Tokens switch
                {
                // shouldn't be required because of the DefaultValueFactory above
                [] => LookupShellFromEnvironment(),
                [var shellToken] => ShellProvidersByArgument[shellToken.Value],
                    _ => throw new InvalidOperationException("Unexpected number of tokens")
                };
            }
        };

        shellArg.AcceptOnlyFromAmong(ShellProvidersByArgument.Keys.ToArray());
        Subcommands.Add(new GenerateScriptCommand(shellArg));
        Subcommands.Add(new RegisterScriptCommand(shellArg));

        static Shells.IShellProvider LookupShellFromEnvironment()
        {
            if (System.OperatingSystem.IsWindows())
            {
                return ShellProvidersByArgument[Shells.PowershellShellProvider.PowerShell];
            }
            var shellPath = Environment.GetEnvironmentVariable("SHELL");
            if (shellPath is null)
            {
                throw new InvalidOperationException("Could not determine the shell from the environment");
            }
            var shellName = Path.GetFileName(shellPath);
            var shellProvider = _knownShells.FirstOrDefault(p => p.ArgumentName.Equals(shellPath, StringComparison.OrdinalIgnoreCase));
            if (shellProvider is null)
            {
                throw new InvalidOperationException($"Shell '{shellPath}' is not supported");
            }

            return shellProvider;
        }
    }
}

public class GenerateScriptCommand : CliCommand
{
    public GenerateScriptCommand(CliArgument<Shells.IShellProvider> shellArg)
        : base("script", "Generate the completion script for a supported shell")
    {
        Arguments.Add(shellArg);
        SetAction(args =>
        {
            var shell = args.GetValue(shellArg);
            var script = shell.GenerateCompletions(Parser.Instance.RootCommand);
            args.Configuration.Output.Write(script);
        });
    }
}

public class RegisterScriptCommand : CliCommand
{
    public RegisterScriptCommand(CliArgument<Shells.IShellProvider> shellArg)
        : base("register", "Register the completion script for a supported shell")
    {
        Arguments.Add(shellArg);
    }
}
