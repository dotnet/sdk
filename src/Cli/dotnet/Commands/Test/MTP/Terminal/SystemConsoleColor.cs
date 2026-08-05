// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

/// <summary>
/// Represents a system console color.
/// </summary>
public sealed class SystemConsoleColor : IColor
{
    /// <summary>
    /// Gets or inits the console color.
    /// </summary>
    public ConsoleColor ConsoleColor { get; init; }
}
