// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.CoreUtils.Tests;

[TestClass]
public sealed class ManagedAssemblyIdentityTests
{
    private static readonly Version s_version = new(1, 2, 3, 4);

    [TestMethod]
    public void ValidateSatelliteOf_MatchingIdentity_DoesNotThrow()
    {
        ManagedAssemblyIdentity owner = new("Owner", s_version, string.Empty, [1, 2, 3]);
        ManagedAssemblyIdentity satellite = new("Owner.resources", s_version, "de", [1, 2, 3]);

        satellite.ValidateSatelliteOf(owner, "de", contractVersion: null);
    }

    [TestMethod]
    public void ValidateSatelliteOf_MismatchedIdentity_ThrowsFileLoadException()
    {
        ManagedAssemblyIdentity owner = new("Owner", s_version, string.Empty, [1, 2, 3]);
        ManagedAssemblyIdentity satellite = new("Owner.resources", new(5, 0), "de", [3, 2, 1]);

        Assert.ThrowsExactly<FileLoadException>(
            () => satellite.ValidateSatelliteOf(owner, "de", contractVersion: null));
    }

    [TestMethod]
    public void ValidateMatches_AliasedNameAndRemainingIdentityMatch()
    {
        ManagedAssemblyIdentity generated = new("generated-owner", s_version, string.Empty, [1, 2, 3]);
        ManagedAssemblyIdentity external = new("external-owner", s_version, string.Empty, [1, 2, 3]);

        external.ValidateMatches(generated.WithName("external-owner"));
    }
}
