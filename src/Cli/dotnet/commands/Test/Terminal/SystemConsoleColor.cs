// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

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
