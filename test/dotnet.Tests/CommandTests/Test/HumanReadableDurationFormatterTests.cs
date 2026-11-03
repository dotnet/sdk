// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public sealed class HumanReadableDurationFormatterTests
{
    [TestMethod]
    [DataRow(null, true, false, "")]
    [DataRow(0L, true, false, "(0s)")]
    [DataRow(1234L, true, false, "(1s)")]
    [DataRow(234L, true, true, "(234ms)")]
    [DataRow(1234L, true, true, "(1s 234ms)")]
    [DataRow(62003L, true, true, "(1m 02s 003ms)")]
    [DataRow(93784005L, true, true, "(1d 02h 03m 04s 005ms)")]
    [DataRow(1000L, false, false, "1s")]
    public void Render_ReturnsExpectedValue(long? milliseconds, bool wrapInParentheses, bool showMilliseconds, string expected)
    {
        TimeSpan? duration = milliseconds.HasValue ? TimeSpan.FromMilliseconds(milliseconds.Value) : null;

        HumanReadableDurationFormatter.Render(duration, wrapInParentheses, showMilliseconds).Should().Be(expected);
    }
}
