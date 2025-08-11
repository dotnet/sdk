// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
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
        Arity = ArgumentArity.Zero,
        Action = new SetDiagnosticModeAction()
    };

    /// <summary>
    /// Sets a few verbose diagnostics flags across the CLI.
    /// Other commands may also use this to set their verbosity flags to a higher value or similar behaviors.
    /// </summary>
    internal class SetDiagnosticModeAction() : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            Environment.SetEnvironmentVariable(CommandLoggingContext.Variables.Verbose, bool.TrueString);
            CommandLoggingContext.SetVerbose(true);
            Reporter.Reset();
            return 0;
        }
    }
}
