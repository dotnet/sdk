// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

/// <summary>
/// Removes ANSI CSI sequences from captured test output without changing live CLI styling.
/// </summary>
internal static partial class ConsoleOutputNormalizer
{
    internal static string StripAnsi(string output) => AnsiEscapeSequenceRegex().Replace(output, string.Empty);

    internal static string Normalize(string output) => WhitespaceRegex().Replace(StripAnsi(output), " ");

    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex AnsiEscapeSequenceRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex WhitespaceRegex();
}
