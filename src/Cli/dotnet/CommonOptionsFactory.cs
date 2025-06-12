// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Creates common options.
/// </summary>
internal static class CommonOptionsFactory
{
    /// <summary>
    /// Creates common diagnostics option (-d|--diagnostics).
    /// </summary>
    public static Option<bool> CreateDiagnosticsOption(bool recursive) => new("--diagnostics", "-d")
    {
        Description = CliStrings.SDKDiagnosticsCommandDefinition,
        Recursive = recursive,
        Arity = ArgumentArity.Zero
    };

    internal class SetDiagnosticModeAction(Option<bool> diagnosticOption) : System.CommandLine.Invocation.SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            if (parseResult.IsDotnetBuiltInCommand())
            {
                var diagIsChildOfRoot = parseResult.RootCommandResult.Children.FirstOrDefault((s) => s is OptionResult opt && opt.Option == diagnosticOption) is not null;

                // We found --diagnostic or -d, but we still need to determine whether the option should
                // be attached to the dotnet command or the subcommand.
                if (diagIsChildOfRoot)
                {
                    Environment.SetEnvironmentVariable(CommandLoggingContext.Variables.Verbose, bool.TrueString);
                    CommandLoggingContext.SetVerbose(true);
                    Reporter.Reset();
                }
            }
            return 0;
        }
    }
}
