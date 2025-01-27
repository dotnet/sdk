// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions;

using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.StaticCompletions.Resources;
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
    /// <param name="commandName">The name of the completions command. Default is "completions". This value is what users will type to activate the command on the command line.</param>
    /// <param name="commandDescription">The description of the completions command. Default is "Commands for generating and registering completions for supported shells".</param>
    public CompletionsCommand(IEnumerable<IShellProvider>? supportedShells = null, string commandName = "completions", string? commandDescription = null) : this((supportedShells ?? DefaultShells).ToDictionary(s => s.ArgumentName, StringComparer.OrdinalIgnoreCase), commandName, commandDescription ?? Strings.CompletionsCommand_Description)
    { }

    private CompletionsCommand(Dictionary<string, IShellProvider> shellMap, string commandName, string commandDescription) : base(commandName, commandDescription)
    {
        var shellArg = new CliArgument<IShellProvider>("shell")
        {
            Description = Strings.CompletionsCommand_ShellArgument_Description,
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
                    _ => throw new InvalidOperationException("Unexpected number of tokens") // this is impossible because of the Arity set above, no need to translate
                };
            }
        };

        shellArg.Validators.Clear();
        shellArg.Validators.Add(OnlyAcceptSupportedShells(shellMap));
        shellArg.CompletionSources.Clear();
        shellArg.CompletionSources.Add(CreateCompletions(shellMap));

        Subcommands.Add(new GenerateScriptCommand(shellArg));

        static IShellProvider LookupShellFromEnvironment(Dictionary<string, IShellProvider> shellMap)
        {
            if (OperatingSystem.IsWindows())
            {
                return shellMap[PowershellShellProvider.PowerShell];
            }
            var shellPath = Environment.GetEnvironmentVariable("SHELL");
            if (shellPath is null)
            {
                throw new InvalidOperationException(Strings.ShellDiscovery_ShellEnvironmentNotSet);
            }
            var shellName = Path.GetFileName(shellPath);
            if (shellMap.TryGetValue(shellPath, out var shellProvider))
            {
                return shellProvider;
            }
            else
            {
                throw new InvalidOperationException(String.Format(Strings.ShellDiscovery_ShellNotSupported, shellName, string.Join(", ", shellMap.Keys)));
            }
        }

        static Action<Parsing.ArgumentResult> OnlyAcceptSupportedShells(Dictionary<string, IShellProvider> shellMap)
        {
            return (Parsing.ArgumentResult argumentResult) =>
            {
                if (argumentResult.Tokens.Count == 0)
                {
                    return;
                }
                var singleToken = argumentResult.Tokens[0];
                if (!shellMap.ContainsKey(singleToken.Value))
                {
                    argumentResult.AddError(String.Format(Strings.ShellDiscovery_ShellNotSupported, singleToken.Value, string.Join(", ", shellMap.Keys)));
                }
            };
        }

        static Func<CompletionContext, IEnumerable<CompletionItem>> CreateCompletions(Dictionary<string, IShellProvider> shellMap)
        {
            return (CompletionContext context) =>
            {
                return shellMap.Values.Select(shellProvider => new CompletionItem(shellProvider.ArgumentName, documentation: shellProvider.HelpDescription));
            };
        }
    }
}

public class GenerateScriptCommand : CliCommand
{
    public GenerateScriptCommand(CliArgument<IShellProvider> shellArg)
        : base("script", Strings.GenerateCommand_Description)
    {
        Arguments.Add(shellArg);
        SetAction(args =>
        {
            IShellProvider shell = args.GetValue(shellArg)!; // this cannot be null due to the way the shellArg is defined/configured
            var script = shell.GenerateCompletions(args.RootCommandResult.Command);
            args.Configuration.Output.Write(script);
        });
    }
}
