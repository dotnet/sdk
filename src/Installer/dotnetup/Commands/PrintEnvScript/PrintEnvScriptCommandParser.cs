// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.PrintEnvScript;

internal static class PrintEnvScriptCommandParser
{
    internal static readonly IEnvShellProvider[] SupportedShells =
    [
        new BashEnvShellProvider(),
        new ZshEnvShellProvider(),
        new PowerShellEnvShellProvider()
    ];

    private static readonly Dictionary<string, IEnvShellProvider> ShellMap =
        SupportedShells.ToDictionary(s => s.ArgumentName, StringComparer.OrdinalIgnoreCase);

    public static readonly Option<IEnvShellProvider?> ShellOption = new("--shell", "-s")
    {
        Description = $"The shell for which to generate the environment script (supported: {string.Join(", ", SupportedShells.Select(s => s.ArgumentName))}). If not specified, the current shell will be detected.",
        Arity = ArgumentArity.ZeroOrOne,
        // called when no token is presented at all
        DefaultValueFactory = (optionResult) => LookupShellFromEnvironment(),
        // called for all other scenarios
        CustomParser = (optionResult) =>
        {
            return optionResult.Tokens switch
            {
                // shouldn't be required because of the DefaultValueFactory above
                [] => LookupShellFromEnvironment(),
                [var shellToken] => ShellMap[shellToken.Value],
                _ => throw new InvalidOperationException("Unexpected number of tokens") // this is impossible because of the Arity set above
            };
        }
    };

    public static readonly Option<string?> DotnetInstallPathOption = new("--dotnet-install-path", "-d")
    {
        Description = "The path to the .NET installation directory. If not specified, uses the default user install path.",
        Arity = ArgumentArity.ZeroOrOne
    };

    static PrintEnvScriptCommandParser()
    {
        // Add validator to only accept supported shells
        ShellOption.Validators.Clear();
        ShellOption.Validators.Add(OnlyAcceptSupportedShells());

        // Add completions for shell names
        ShellOption.CompletionSources.Clear();
        ShellOption.CompletionSources.Add(CreateCompletions());
    }

    private static readonly Command PrintEnvScriptCommand = ConstructCommand();

    public static Command GetCommand()
    {
        return PrintEnvScriptCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("print-env-script", "Generates a shell script that configures the environment for .NET");

        command.Options.Add(ShellOption);
        command.Options.Add(DotnetInstallPathOption);

        command.SetAction(parseResult => new PrintEnvScriptCommand(parseResult).Execute());

        return command;
    }

    private static IEnvShellProvider? LookupShellFromEnvironment()
    {
        if (OperatingSystem.IsWindows())
        {
            return ShellMap["pwsh"];
        }

        var shellPath = Environment.GetEnvironmentVariable("SHELL");
        if (shellPath is null)
        {
            // Return null if we can't detect the shell
            // This allows help to work, but Execute will handle the error
            return null;
        }

        var shellName = Path.GetFileName(shellPath);
        if (ShellMap.TryGetValue(shellName, out var shellProvider))
        {
            return shellProvider;
        }
        else
        {
            // Return null for unsupported shells
            // This allows help to work, but Execute will handle the error
            return null;
        }
    }

    private static Action<System.CommandLine.Parsing.OptionResult> OnlyAcceptSupportedShells()
    {
        return (System.CommandLine.Parsing.OptionResult optionResult) =>
        {
            if (optionResult.Tokens.Count == 0)
            {
                return;
            }
            var singleToken = optionResult.Tokens[0];
            if (!ShellMap.ContainsKey(singleToken.Value))
            {
                optionResult.AddError($"Unsupported shell '{singleToken.Value}'. Supported shells: {string.Join(", ", ShellMap.Keys)}");
            }
        };
    }

    private static Func<CompletionContext, IEnumerable<CompletionItem>> CreateCompletions()
    {
        return (CompletionContext context) =>
        {
            return ShellMap.Values.Select(shellProvider => new CompletionItem(shellProvider.ArgumentName, documentation: shellProvider.HelpDescription));
        };
    }
}
