// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions;

using System.CommandLine;
using System.CommandLine.StaticCompletions.Shells;
using System.Linq;

public class CompletionsCommand : CliCommand
{
    public static IShellProvider[] DefaultShells = [
        new BashShellProvider(),
        new PowershellShellProvider(),
        new FishShellProvider(),
        new ZshShellProvider(),
        new NushellShellProvider()
    ];

    /// <summary>
    /// Creates a new Completions command exclusively with the provided supported shells. If no shells are provided, the <see cref="DefaultShells"/> are used.
    /// </summary>
    /// <param name="supportedShells">The shells to support in the completions command. If null, <see cref="DefaultShells"/> will be used.</param>
    public CompletionsCommand(IEnumerable<IShellProvider>? supportedShells = null) : this((supportedShells ?? DefaultShells).ToDictionary(s => s.ArgumentName, StringComparer.OrdinalIgnoreCase))
    { }

    private CompletionsCommand(Dictionary<string, IShellProvider> shellMap)
        : base("completions", "Commands for generating and registering completions for supported shells")
    {
        var shellArg = new CliArgument<IShellProvider>("shell")
        {
            Description = "The shell for which to generate or register completions",
            Arity = ArgumentArity.ZeroOrOne,
            // called when no token is presented at all
            DefaultValueFactory = (argResult) => LookupShellFromEnvironment(shellMap),
            // called for all other scenarios
            CustomParser = (argResult) =>
            {
                return argResult.Tokens switch
                {
                // shouldn't be required because of the DefaultValueFactory above
                [] => LookupShellFromEnvironment(shellMap),
                [var shellToken] => shellMap[shellToken.Value],
                    _ => throw new InvalidOperationException("Unexpected number of tokens")
                };
            }
        };

        shellArg.AcceptOnlyFromAmong(shellMap.Keys.ToArray());
        Subcommands.Add(new GenerateScriptCommand(shellArg));
        Subcommands.Add(new RegisterScriptCommand(shellArg));

        static IShellProvider LookupShellFromEnvironment(Dictionary<string, IShellProvider> shellMap)
        {
            if (OperatingSystem.IsWindows())
            {
                return shellMap[PowershellShellProvider.PowerShell];
            }
            var shellPath = Environment.GetEnvironmentVariable("SHELL");
            if (shellPath is null)
            {
                throw new InvalidOperationException("Could not determine the shell from the environment");
            }
            var shellName = Path.GetFileName(shellPath);
            if (shellMap.TryGetValue(shellPath, out var shellProvider))
            {
                return shellProvider;
            }
            else
            {
                throw new InvalidOperationException($"Shell '{shellPath}' is not supported");
            }

            return shellProvider;
        }
    }
}

public class GenerateScriptCommand : CliCommand
{
    public GenerateScriptCommand(CliArgument<IShellProvider> shellArg)
        : base("script", "Generate the completion script for a supported shell")
    {
        Arguments.Add(shellArg);
        SetAction(args =>
        {
            var shell = args.GetValue(shellArg);
            if (shell is null)
            {
                throw new InvalidOperationException("No shell was provided");
            }
            var script = shell.GenerateCompletions(args.RootCommandResult.Command);
            args.Configuration.Output.Write(script);
        });
    }
}

public class RegisterScriptCommand : CliCommand
{
    public RegisterScriptCommand(CliArgument<IShellProvider> shellArg)
        : base("register", "Register the completion script for a supported shell")
    {
        Arguments.Add(shellArg);
    }
}
