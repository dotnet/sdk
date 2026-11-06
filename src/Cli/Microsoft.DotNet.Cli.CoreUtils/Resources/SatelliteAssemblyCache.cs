// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Caches runtime satellite binding results for one resource assembly.
/// </summary>
internal sealed class SatelliteAssemblyCache
{
    private readonly Dictionary<string, Assembly?> _assemblies = [with(StringComparer.Ordinal)];
    private readonly Lock _lock = new();
    private SatelliteStringResourceSourceMetadata? _metadata;
    private SatelliteStringResourceSourceMetadata? _runtimeMetadata;

    /// <summary>
    ///  Gets cached source metadata for the resource assembly.
    /// </summary>
    /// <param name="resourceAssembly">The main resource assembly.</param>
    /// <param name="includeContractVersion">
    ///  Whether to read and validate <see cref="SatelliteContractVersionAttribute"/>.
    /// </param>
    /// <returns>The cached source metadata.</returns>
    internal SatelliteStringResourceSourceMetadata GetMetadata(
        Assembly resourceAssembly,
        bool includeContractVersion)
    {
        lock (_lock)
        {
            SatelliteStringResourceSourceMetadata? metadata = includeContractVersion
                ? _runtimeMetadata
                : _metadata;

            if (metadata is not null)
            {
                return metadata;
            }

            string? assemblyName = resourceAssembly.GetName().Name;
            ManagedAssemblyIdentity resourceAssemblyIdentity = ManagedAssemblyIdentity.FromAssemblyName(
                resourceAssembly.GetName());

            string? satelliteAssemblyFileName = assemblyName is null
                ? null
                : $"{assemblyName}.resources.dll";

            string? neutralCultureName = null;
            Attribute? neutralAttribute = Attribute.GetCustomAttribute(
                resourceAssembly,
                typeof(NeutralResourcesLanguageAttribute));

            if (neutralAttribute is NeutralResourcesLanguageAttribute neutralResources)
            {
                if (neutralResources.Location == UltimateResourceFallbackLocation.Satellite)
                {
                    throw new NotSupportedException("Neutral resources stored in a satellite are not supported.");
                }

                neutralCultureName = neutralResources.CultureName;
            }

            Version? satelliteContractVersion = null;
            if (includeContractVersion)
            {
                Attribute? contractAttribute = Attribute.GetCustomAttribute(
                    resourceAssembly,
                    typeof(SatelliteContractVersionAttribute));

                if (contractAttribute is SatelliteContractVersionAttribute contract
                    && !Version.TryParse(contract.Version, out satelliteContractVersion))
                {
                    throw new ArgumentException(
                        "The satellite contract version is invalid.",
                        nameof(resourceAssembly));
                }
            }

            metadata = new(
                resourceAssemblyIdentity,
                satelliteAssemblyFileName,
                neutralCultureName,
                satelliteContractVersion);

            if (includeContractVersion)
            {
                _runtimeMetadata = metadata;
            }
            else
            {
                _metadata = metadata;
            }

            return metadata;
        }
    }

    /// <summary>
    ///  Gets the satellite assembly for <paramref name="culture"/>, caching both successful and
    ///  missing binds.
    /// </summary>
    /// <param name="resourceAssembly">The main resource assembly.</param>
    /// <param name="culture">The satellite culture.</param>
    /// <param name="contractVersion">The satellite contract version, if declared.</param>
    /// <returns>The satellite assembly, or <see langword="null"/> when it is absent.</returns>
    internal Assembly? GetSatelliteAssembly(
        Assembly resourceAssembly,
        CultureInfo culture,
        Version? contractVersion)
    {
        lock (_lock)
        {
            if (_assemblies.TryGetValue(culture.Name, out Assembly? satellite))
            {
                return satellite;
            }

            try
            {
                satellite = contractVersion is null
                    ? resourceAssembly.GetSatelliteAssembly(culture)
                    : resourceAssembly.GetSatelliteAssembly(culture, contractVersion);
            }
            catch (FileNotFoundException)
            {
                satellite = null;
            }

            _assemblies.Add(culture.Name, satellite);
            return satellite;
        }
    }
}
