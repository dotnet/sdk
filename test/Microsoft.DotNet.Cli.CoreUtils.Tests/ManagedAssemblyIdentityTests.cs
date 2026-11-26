// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.CoreUtils.Tests;

[TestClass]
public sealed class ManagedAssemblyIdentityTests
{
    private static readonly Version s_version = new(1, 2, 3, 4);

    [TestMethod]
    [DataRow(0)]
    [DataRow(8)]
    public void Constructor_ValidPublicKeyTokenLength_DoesNotThrow(int length)
    {
        _ = new ManagedAssemblyIdentity("Owner", s_version, string.Empty, new byte[length]);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(7)]
    [DataRow(9)]
    public void Constructor_InvalidPublicKeyTokenLength_ThrowsBadImageFormatException(int length)
    {
        Assert.ThrowsExactly<BadImageFormatException>(
            () => new ManagedAssemblyIdentity("Owner", s_version, string.Empty, new byte[length]));
    }

    [TestMethod]
    public void Constructor_NullPublicKeyToken_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new ManagedAssemblyIdentity("Owner", s_version, string.Empty, null!));
    }

    [TestMethod]
    public void ValidateSatelliteOf_MatchingIdentity_DoesNotThrow()
    {
        ManagedAssemblyIdentity owner = new("Owner", s_version, string.Empty, [1, 2, 3, 4, 5, 6, 7, 8]);
        ManagedAssemblyIdentity satellite = new("Owner.resources", s_version, "de", [1, 2, 3, 4, 5, 6, 7, 8]);

        satellite.ValidateSatelliteOf(owner, "de", contractVersion: null);
    }

    [TestMethod]
    public void ValidateSatelliteOf_MismatchedIdentity_ThrowsFileLoadException()
    {
        ManagedAssemblyIdentity owner = new("Owner", s_version, string.Empty, [1, 2, 3, 4, 5, 6, 7, 8]);
        ManagedAssemblyIdentity satellite = new("Owner.resources", new(5, 0), "de", [8, 7, 6, 5, 4, 3, 2, 1]);

        Assert.ThrowsExactly<FileLoadException>(
            () => satellite.ValidateSatelliteOf(owner, "de", contractVersion: null));
    }

    [TestMethod]
    public void ValidateMatches_AliasedNameAndRemainingIdentityMatch()
    {
        ManagedAssemblyIdentity generated =
            new("generated-owner", s_version, string.Empty, [1, 2, 3, 4, 5, 6, 7, 8]);
        ManagedAssemblyIdentity external =
            new("external-owner", s_version, string.Empty, [1, 2, 3, 4, 5, 6, 7, 8]);

        external.ValidateMatches(generated.WithName("external-owner"));
    }
}
