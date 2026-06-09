// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;

namespace Microsoft.DotNet.Cli;

internal class HandleDiagnosticAction(Option<bool> option) : InvocableOptionAction(option)
{
    public override bool Terminating => false;

    // S.CL will always invoke this option action because:
    // 1. A boolean option has a default value of `false`.
    // 2. The option has Terminating set to `false`.
    // When a default value exists (non-null) and the option is not terminating, the option action is always invoked.
    public override int Invoke(ParseResult parseResult)
    {
        // This check is necessary as per comment above.
        if (!parseResult.HasOption(Option) || !parseResult.GetValue(option)
            // Only set verbose output on built-in commands.
            || !parseResult.IsDotnetBuiltInCommand())
        {
            return 0;
        }

        // Determine whether the diagnostic option should be attached to the dotnet command or the subcommand.
        if (OptionPrecedesSubcommand(parseResult.Tokens.Select(t => t.Value), parseResult.RootSubCommandResult()))
        {
            Environment.SetEnvironmentVariable(CommandLoggingContext.Variables.Verbose, bool.TrueString);
            CommandLoggingContext.SetVerbose(true);
            Reporter.Reset();

            var home = Env.GetEnvironmentVariable(CliFolderPathCalculator.DotnetHomeVariableName);
            if (!string.IsNullOrEmpty(home))
            {
                // Output DOTNET_CLI_HOME usage when verbosity is enabled.
                Reporter.Verbose.WriteLine(string.Format(LocalizableStrings.DotnetCliHomeUsed, home, CliFolderPathCalculator.DotnetHomeVariableName));
            }
        }

        return 0;
    }

    private bool OptionPrecedesSubcommand(IEnumerable<string> tokens, string subCommand)
    {
        if (string.IsNullOrEmpty(subCommand))
        {
            return true;
        }

        foreach (var token in tokens)
        {
            if (token == subCommand)
            {
                return false;
            }

            if (Option.Name == token || Option.Aliases.Contains(token))
            {
                return true;
            }
        }

        return false;
    }
}

internal class PrintCliSchemaAction(Option<bool> option) : InvocableOptionAction(option)
{
    public override bool Terminating => true;

    public override int Invoke(ParseResult parseResult)
    {
        if (!parseResult.HasOption(Option) || !parseResult.GetValue(option))
        {
            return 0;
        }

        CliSchema.PrintCliSchema(parseResult, parseResult.InvocationConfiguration.Output, Program.TelemetryInstance);

        return 0;
    }
}
