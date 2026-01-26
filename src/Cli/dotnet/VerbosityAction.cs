// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Action that sets DOTNET_CLI_CONTEXT_VERBOSE environment variable when verbosity is diagnostic.
/// </summary>
internal class ApplyVerbosityAction<T> : SynchronousCommandLineAction
{
    private readonly Option<T> _verbosityOption;

    public ApplyVerbosityAction(Option<T> verbosityOption)
    {
        _verbosityOption = verbosityOption;
    }

    public override bool Terminating => false;

    public override int Invoke(ParseResult parseResult)
    {
        var value = parseResult.GetValue(_verbosityOption);
        
        // Handle both VerbosityOptions and VerbosityOptions?
        if (value is VerbosityOptions verbosity)
        {
            if (verbosity.IsDiagnostic())
            {
                Environment.SetEnvironmentVariable(CommandLoggingContext.Variables.Verbose, bool.TrueString);
                CommandLoggingContext.SetVerbose(true);
                Reporter.Reset();
            }
        }

        return 0;
    }
}
