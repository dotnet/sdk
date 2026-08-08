// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class SourceDateEpochTests
{
    private static Func<string, string?> Env(string? value) => _ => value;

    [TestMethod]
    public void GetTimestamp_ReturnsTheInstantIdentifiedBySourceDateEpoch()
    {
        // 2021-11-08T12:34:56Z, a value chosen only because it is unambiguous in UTC.
        DateTime timestamp = SourceDateEpoch.GetTimestamp(Env("1636374896"));

        Assert.AreEqual(new DateTime(2021, 11, 8, 12, 34, 56, DateTimeKind.Utc), timestamp);
        Assert.AreEqual(DateTimeKind.Utc, timestamp.Kind);
    }

    [TestMethod]
    public void GetTimestamp_AcceptsSurroundingWhitespace()
    {
        Assert.AreEqual(
            new DateTime(2021, 11, 8, 12, 34, 56, DateTimeKind.Utc),
            SourceDateEpoch.GetTimestamp(Env("  1636374896\n")));
    }

    [TestMethod]
    public void GetTimestamp_AcceptsTheUnixEpochItself()
    {
        Assert.AreEqual(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), SourceDateEpoch.GetTimestamp(Env("0")));
    }

    [TestMethod]
    public void GetTimestamp_IsStableAcrossCalls()
    {
        Assert.AreEqual(SourceDateEpoch.GetTimestamp(Env("1636374896")), SourceDateEpoch.GetTimestamp(Env("1636374896")));
    }

    [TestMethod]
    [DataRow(null, DisplayName = "unset")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("   ", DisplayName = "whitespace")]
    [DataRow("not-a-number", DisplayName = "not numeric")]
    [DataRow("1636374896.5", DisplayName = "fractional")]
    [DataRow("-1", DisplayName = "negative")]
    [DataRow("0x10", DisplayName = "hexadecimal")]
    [DataRow("1,636,374,896", DisplayName = "group separators")]
    [DataRow("99999999999999999999", DisplayName = "larger than Int64")]
    [DataRow("253402300800", DisplayName = "beyond DateTimeOffset's range")]
    public void GetTimestamp_FallsBackToTheCurrentTimeForValuesItCannotInterpret(string? value)
    {
        // The specification asks consumers to ignore a value they cannot interpret rather than fail,
        // so an unusable value must behave exactly as if the variable were never set.
        DateTime before = DateTime.UtcNow;
        DateTime timestamp = SourceDateEpoch.GetTimestamp(Env(value));
        DateTime after = DateTime.UtcNow;

        Assert.IsTrue(timestamp >= before && timestamp <= after, $"Expected the current time, but got {timestamp:o}.");
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void GetTimestamp_ReadsSourceDateEpochFromTheEnvironmentByDefault()
    {
        // The production call sites pass no reader, so the default path is worth covering directly.
        string? original = Environment.GetEnvironmentVariable("SOURCE_DATE_EPOCH");
        try
        {
            Environment.SetEnvironmentVariable("SOURCE_DATE_EPOCH", "1636374896");
            Assert.AreEqual(new DateTime(2021, 11, 8, 12, 34, 56, DateTimeKind.Utc), SourceDateEpoch.GetTimestamp());
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOURCE_DATE_EPOCH", original);
        }
    }

    [TestMethod]
    public void GetTimestamp_IsNotAffectedByTheCurrentCulture()
    {
        // A culture with different number formatting must not change how the value is parsed.
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.AreEqual(
                new DateTime(2021, 11, 8, 12, 34, 56, DateTimeKind.Utc),
                SourceDateEpoch.GetTimestamp(Env("1636374896")));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
