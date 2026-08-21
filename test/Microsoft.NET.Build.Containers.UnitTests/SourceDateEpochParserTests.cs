// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Tasks;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class SourceDateEpochParserTests
{
    [TestMethod]
    [DataRow("0", 0L)]
    [DataRow("1636374896", 1636374896L)]
    [DataRow("99999999999", 99999999999L)]
    [DataRow("100000000000", 100000000000L)]
    public void ParseReturnsUtcTimestamp(string value, long expectedSeconds)
    {
        DateTime? actual = SourceDateEpochParser.Parse(value);

        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(expectedSeconds).UtcDateTime, actual);
        Assert.AreEqual(DateTimeKind.Utc, actual!.Value.Kind);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "unset")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("   ", DisplayName = "whitespace")]
    [DataRow("  1636374896\n", DisplayName = "surrounding whitespace")]
    [DataRow("not-a-number", DisplayName = "not numeric")]
    [DataRow("1636374896.5", DisplayName = "fractional")]
    [DataRow("-1", DisplayName = "negative")]
    [DataRow("0x10", DisplayName = "hexadecimal")]
    [DataRow("1,636,374,896", DisplayName = "group separators")]
    [DataRow("99999999999999999999", DisplayName = "larger than Int64")]
    [DataRow("253402300800", DisplayName = "outside DateTimeOffset's range")]
    public void ParseReturnsNullForInvalidValues(string? value)
        => Assert.IsNull(SourceDateEpochParser.Parse(value));
}
