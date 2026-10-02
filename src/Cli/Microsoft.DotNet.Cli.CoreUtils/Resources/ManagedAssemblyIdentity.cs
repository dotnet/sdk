// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Reflection.Metadata;

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  The assembly-definition identity needed to validate a satellite assembly.
/// </summary>
internal sealed class ManagedAssemblyIdentity
{
    private readonly byte[] _publicKeyToken;

    /// <summary>
    ///  Initializes an assembly identity.
    /// </summary>
    /// <param name="name">The simple assembly name.</param>
    /// <param name="version">The assembly version.</param>
    /// <param name="cultureName">The assembly culture name, or an empty string for neutral.</param>
    /// <param name="publicKeyToken">The public key token, or an empty array for an unsigned assembly.</param>
    internal ManagedAssemblyIdentity(
        string name,
        Version version,
        string cultureName,
        byte[] publicKeyToken)
    {
        Name = name;
        Version = version;
        CultureName = cultureName;
        _publicKeyToken = publicKeyToken;
    }

    /// <summary>
    ///  The simple assembly name.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    ///  The assembly version.
    /// </summary>
    internal Version Version { get; }

    /// <summary>
    ///  The assembly culture name, or an empty string for neutral.
    /// </summary>
    internal string CultureName { get; }

    /// <summary>
    ///  Creates an identity from an already-loaded assembly name.
    /// </summary>
    /// <param name="assemblyName">The assembly name.</param>
    /// <returns>The normalized identity.</returns>
    internal static ManagedAssemblyIdentity FromAssemblyName(AssemblyName assemblyName)
    {
        string name = assemblyName.Name
            ?? throw new BadImageFormatException("The assembly does not have a simple name.");

        Version version = assemblyName.Version
            ?? throw new BadImageFormatException("The assembly does not have a version.");

        byte[] publicKeyToken = assemblyName.GetPublicKeyToken() ?? [];
        return new(
            name,
            version,
            assemblyName.CultureName ?? string.Empty,
            publicKeyToken);
    }

    /// <summary>
    ///  Creates an identity from managed assembly metadata.
    /// </summary>
    /// <param name="metadataReader">The metadata reader.</param>
    /// <returns>The assembly-definition identity.</returns>
    internal static ManagedAssemblyIdentity FromMetadata(MetadataReader metadataReader)
    {
        if (!metadataReader.IsAssembly)
        {
            throw new BadImageFormatException("The managed image does not contain an assembly definition.");
        }

        AssemblyDefinition definition = metadataReader.GetAssemblyDefinition();
        string name = metadataReader.GetString(definition.Name);
        if (name.Length == 0)
        {
            throw new BadImageFormatException("The assembly definition does not have a simple name.");
        }

        string cultureName = definition.Culture.IsNil
            ? string.Empty
            : metadataReader.GetString(definition.Culture);

        byte[] publicKeyToken = definition.PublicKey.IsNil
            ? []
            : GetPublicKeyToken(
                metadataReader.GetBlobBytes(definition.PublicKey),
                definition.Flags.AreFlagsSet(AssemblyFlags.PublicKey));

        return new(
            name,
            definition.Version,
            cultureName,
            publicKeyToken);
    }

    /// <summary>
    ///  Creates an identity with a different simple name and the same remaining identity fields.
    /// </summary>
    /// <param name="name">The replacement simple name.</param>
    /// <returns>The renamed identity.</returns>
    internal ManagedAssemblyIdentity WithName(string name) =>
        new(name, Version, CultureName, _publicKeyToken);

    /// <summary>
    ///  Validates that this identity is a satellite of <paramref name="resourceAssembly"/>.
    /// </summary>
    /// <param name="resourceAssembly">The resource-owning assembly identity.</param>
    /// <param name="cultureName">The expected satellite culture.</param>
    /// <param name="contractVersion">The declared satellite contract version, if any.</param>
    internal void ValidateSatelliteOf(
        ManagedAssemblyIdentity resourceAssembly,
        string cultureName,
        Version? contractVersion)
    {
        string expectedName = $"{resourceAssembly.Name}.resources";
        Version expectedVersion = contractVersion ?? resourceAssembly.Version;
        if (string.Equals(Name, expectedName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(CultureName, cultureName, StringComparison.OrdinalIgnoreCase)
            && Version.Equals(expectedVersion)
            && _publicKeyToken.AsSpan().SequenceEqual(resourceAssembly._publicKeyToken))
        {
            return;
        }

        string actualCulture = CultureName.Length == 0 ? "neutral" : CultureName;
        string expectedCulture = cultureName.Length == 0 ? "neutral" : cultureName;
        throw new FileLoadException(
            $"The satellite assembly identity '{Name}, Version={Version}, Culture={actualCulture}' "
                + $"does not match '{expectedName}, Version={expectedVersion}, Culture={expectedCulture}'.");
    }

    /// <summary>
    ///  Validates that this identity matches <paramref name="expected"/>.
    /// </summary>
    /// <param name="expected">The expected assembly identity.</param>
    internal void ValidateMatches(ManagedAssemblyIdentity expected)
    {
        if (string.Equals(Name, expected.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(CultureName, expected.CultureName, StringComparison.OrdinalIgnoreCase)
            && Version.Equals(expected.Version)
            && _publicKeyToken.AsSpan().SequenceEqual(expected._publicKeyToken))
        {
            return;
        }

        string actualCulture = CultureName.Length == 0 ? "neutral" : CultureName;
        string expectedCulture = expected.CultureName.Length == 0 ? "neutral" : expected.CultureName;
        throw new FileLoadException(
            $"The resource assembly identity '{Name}, Version={Version}, Culture={actualCulture}' "
                + $"does not match '{expected.Name}, Version={expected.Version}, Culture={expectedCulture}'.");
    }

    private static byte[] GetPublicKeyToken(byte[] publicKeyOrToken, bool containsPublicKey)
    {
        if (!containsPublicKey)
        {
            return publicKeyOrToken;
        }

        AssemblyName assemblyName = new();
        assemblyName.SetPublicKey(publicKeyOrToken);
        return assemblyName.GetPublicKeyToken()
            ?? throw new BadImageFormatException("The assembly public key is invalid.");
    }
}
