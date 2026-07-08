// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Utilities;

public static class TerminalLoggerExtensions
{
    /// <summary>
    /// Strip out progress markers that TerminalLogger writes to stdout (at least on Windows OS's).
    /// This is non-visible, but impacts string comparison.
    public static string StripTerminalLoggerProgressIndicators(this string stdout)
    {
        return stdout
            .Replace("\x1b]9;4;3;\x1b\\", "") // indeterminate progress start
            .Replace("\x1b]9;4;0;\x1b\\", "") // indeterminate progress end
            .Replace("\x1b[?25l", "") // make cursor invisble
            .Replace("\x1b[?25h", ""); // make cursor visible
    }
}
