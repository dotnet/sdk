// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli;

internal static class CommandExtensions
{
    public static Command GetRootCommand(this Command command)
        => command.Parents.FirstOrDefault(p => p is Command) is Command parentCommand ? GetRootCommand(parentCommand) : command;
}
