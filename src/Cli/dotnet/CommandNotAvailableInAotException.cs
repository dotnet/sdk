// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Thrown when a command is not available in AOT mode and requires
/// fallback to the managed CLI.
/// </summary>
internal sealed class CommandNotAvailableInAotException : Exception
{
    public CommandNotAvailableInAotException()
        : base("This command is not available in AOT mode.")
    {
    }
}
