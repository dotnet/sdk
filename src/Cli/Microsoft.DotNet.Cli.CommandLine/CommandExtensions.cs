// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.CommandLine;

/// <summary>
/// Extensions that make it easy to wire up cancellable command actions without requiring
/// every command handler in the CLI to be written against the asynchronous System.CommandLine
/// APIs. The synchronous <paramref name="action"/> still receives the <see cref="CancellationToken"/>
/// that System.CommandLine produces (for example when a Ctrl+C/SIGTERM is observed), so that it
/// can be checked or forwarded to cancellable APIs as appropriate.
/// </summary>
public static class CommandExtensions
{
    extension(Command command)
    {
        /// <summary>
        /// Sets the action for the command to a synchronous delegate that accepts the
        /// <see cref="CancellationToken"/> supplied by System.CommandLine's invocation pipeline.
        /// </summary>
        public void SetAction(Func<ParseResult, CancellationToken, int> action)
        {
            command.SetAction((parseResult, cancellationToken) => Task.FromResult(action(parseResult, cancellationToken)));
        }
    }
}
