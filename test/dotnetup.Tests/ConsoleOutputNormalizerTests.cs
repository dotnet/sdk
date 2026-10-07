// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Verifies that captured-output comparisons ignore ANSI formatting but preserve literal message text.
/// </summary>
[TestClass]
public class ConsoleOutputNormalizerTests
{
    [TestMethod]
    [DataRow("will \u001b[1mnot\u001b[0m be uninstalled", "will not be uninstalled")]
    [DataRow("will be \u001b[1muninstalled\u001b[0m", "will be uninstalled")]
    [DataRow("will \u001b[1malso\u001b[0m be \u001b[1muninstalled\u001b[0m", "will also be uninstalled")]
    [DataRow("\u001b[38;2;255;0;128m11.0.100\u001b[0m", "11.0.100")]
    [DataRow("\u001b[?25l11.0.100\u001b[?25h", "11.0.100")]
    [DataRow("path [repository]\\global.json", "path [repository]\\global.json")]
    [DataRow("", "")]
    public void StripAnsi_PreservesVisibleText(string output, string expected)
        => ConsoleOutputNormalizer.StripAnsi(output).Should().Be(expected);

    [TestMethod]
    public void StripAnsi_PreservesWhitespace()
        => ConsoleOutputNormalizer.StripAnsi("\t\u001b[1mnot\u001b[0m\r\n").Should().Be("\tnot\r\n");

    [TestMethod]
    public void Normalize_StripsFormattingBeforeCollapsingWhitespace()
        => ConsoleOutputNormalizer.Normalize("will \u001b[1mnot\u001b[0m\r\n  be\tuninstalled")
            .Should().Be("will not be uninstalled");
}
