// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Lazily computed assembly metadata used for satellite probing.
/// </summary>
internal sealed class SatelliteStringResourceSourceMetadata
{
    /// <summary>
    ///  Initializes the source metadata.
    /// </summary>
    /// <param name="resourceAssemblyIdentity">The resource-owning assembly identity, if available.</param>
    /// <param name="satelliteAssemblyFileName">The standard satellite assembly filename.</param>
    /// <param name="neutralCultureName">The declared neutral culture name.</param>
    /// <param name="satelliteContractVersion">The satellite contract version, if declared.</param>
    internal SatelliteStringResourceSourceMetadata(
        ManagedAssemblyIdentity? resourceAssemblyIdentity,
        string? satelliteAssemblyFileName,
        string? neutralCultureName,
        Version? satelliteContractVersion)
    {
        ResourceAssemblyIdentity = resourceAssemblyIdentity;
        SatelliteAssemblyFileName = satelliteAssemblyFileName;
        NeutralCultureName = neutralCultureName;
        SatelliteContractVersion = satelliteContractVersion;
    }

    /// <summary>
    ///  The resource-owning assembly identity, if available.
    /// </summary>
    internal ManagedAssemblyIdentity? ResourceAssemblyIdentity { get; }

    /// <summary>
    ///  The standard satellite assembly filename, if the source assembly has a name.
    /// </summary>
    internal string? SatelliteAssemblyFileName { get; }

    /// <summary>
    ///  The declared neutral culture name, if any.
    /// </summary>
    internal string? NeutralCultureName { get; }

    /// <summary>
    ///  The satellite contract version, if declared.
    /// </summary>
    internal Version? SatelliteContractVersion { get; }

    /// <summary>
    ///  Creates metadata for an external assembly family name while retaining the generated owner's
    ///  version, culture, public-key token, and resource attributes.
    /// </summary>
    /// <param name="simpleName">The external owner assembly simple name.</param>
    /// <returns>The aliased source metadata.</returns>
    internal SatelliteStringResourceSourceMetadata WithSimpleName(string simpleName)
    {
        ManagedAssemblyIdentity identity = ResourceAssemblyIdentity
            ?? throw new InvalidOperationException("The resource assembly identity is missing.");

        return new(
            identity.WithName(simpleName),
            $"{simpleName}.resources.dll",
            NeutralCultureName,
            SatelliteContractVersion);
    }
}