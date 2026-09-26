// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.DotNet.Cli.CoreUtils.Tests;

[TestClass]
public sealed class RawResourceReaderTests
{
    [TestMethod]
    public void Constructor_BadMagic_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _ = new RawResourceReader(new byte[64]));
    }

    [TestMethod]
    public void Constructor_TruncatedHeader_ThrowsBadImageFormatException()
    {
        byte[] valid = ResourceTestUtilities.WriteResources(("Greeting", "Hello"));
        byte[] truncated = valid.AsSpan(0, 10).ToArray();

        Assert.ThrowsExactly<BadImageFormatException>(() => _ = new RawResourceReader(truncated));
    }

    [TestMethod]
    public void TryGetResourceName_NameLengthExceedsImage_ThrowsBadImageFormatException()
    {
        byte[] resources = ResourceTestUtilities.WriteResources(("resource-name", "value"));
        byte[] encodedName = Encoding.Unicode.GetBytes("resource-name");
        int nameStart = resources.AsSpan().IndexOf(encodedName);
        Assert.IsGreaterThan(0, nameStart);

        resources[nameStart - 1] = 126;
        using RawResourceReader reader = new(resources);

        Assert.ThrowsExactly<BadImageFormatException>(
            () => reader.TryGetResourceName(0, new char[8], out _));
    }

    [TestMethod]
    public void GetLocation_IndexOutsideTable_ThrowsArgumentOutOfRangeException()
    {
        using RawResourceReader reader = new(
            ResourceTestUtilities.WriteResources(("Greeting", "Hello")));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => reader.GetLocation(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => reader.GetLocation(1));
    }
}
