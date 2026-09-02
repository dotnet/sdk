// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using CommandResult = System.CommandLine.Parsing.CommandResult;

namespace Microsoft.DotNet.Cli;

/// <summary>
///  Shared command-invocation logic used by both the managed CLI entry point (<see cref="Program"/>)
///  and the NativeAOT bridge (<c>NativeEntryPoint</c>). Keeping this in a single type guarantees the
///  two entry points stay in parity instead of each maintaining their own copy of the invocation and
///  exit-code handling.
/// </summary>
internal static class CommandInvocation
{
    /// <summary>
    ///  Invokes a built-in command in-process via <see cref="Parser.Invoke(ParseResult)"/>, applying the
    ///  same exit-code handling on both the managed CLI and the NativeAOT bridge (including the "new"
    ///  command's 127 adjustment and <see cref="Parser.ExceptionHandler"/>). Errors are reported and
    ///  converted to an exit code, except that under NativeAOT a <see cref="CommandNotAvailableInAotException"/>
    ///  is allowed to propagate so the bridge can fall back to hosting the managed CLI.
    /// </summary>
    internal static int ExecuteInternalCommand(ParseResult parseResult)
    {
        Debug.Assert(parseResult.CanBeInvoked());
        int exitCode;
        using var _ = Activities.Source.StartActivity("invocation");
        try
        {
            exitCode = Parser.Invoke(parseResult);
            if (parseResult.Errors.Any())
            {
                exitCode = AdjustExitCodeForNew();
            }
        }
#if CLI_AOT
        catch (CommandNotAvailableInAotException)
        {
            // The parsed command is parse-only under NativeAOT and must run in the managed CLI. Let the
            // native entry point catch this and fall back to hosting dotnet.dll.
            throw;
        }
#endif
        catch (Exception exception)
        {
            exitCode = Parser.ExceptionHandler(exception, parseResult);
        }
        return exitCode;

        int AdjustExitCodeForNew()
        {
            var commandResult = parseResult.CommandResult;
            while (commandResult is not null)
            {
                if (commandResult.Command.Name == "new")
                {
                    // Default parse error exit code is 1.
                    // For the "new" command and its subcommands, it needs to be 127.
                    return 127;
                }
                commandResult = commandResult.Parent as CommandResult;
            }
            return exitCode;
        }
    }
}
