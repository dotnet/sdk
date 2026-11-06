// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection.Metadata;
using System.Resources;

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Reads resource-binding metadata without loading the managed assembly.
/// </summary>
internal static class ManagedAssemblyMetadataReader
{
    private const string ResourcesNamespace = "System.Resources";
    private const string NeutralResourcesLanguageAttributeName = "NeutralResourcesLanguageAttribute";
    private const string SatelliteContractVersionAttributeName = "SatelliteContractVersionAttribute";

    /// <summary>
    ///  Reads the owner identity and resource-binding attributes.
    /// </summary>
    /// <param name="metadataReader">The managed metadata reader.</param>
    /// <returns>The resource source metadata.</returns>
    internal static SatelliteStringResourceSourceMetadata ReadResourceAssembly(MetadataReader metadataReader)
    {
        ManagedAssemblyIdentity identity = ManagedAssemblyIdentity.FromMetadata(metadataReader);
        string? neutralCultureName = null;
        Version? satelliteContractVersion = null;
        bool foundNeutralResourcesLanguage = false;
        bool foundSatelliteContractVersion = false;

        AssemblyDefinition definition = metadataReader.GetAssemblyDefinition();
        foreach (CustomAttributeHandle handle in definition.GetCustomAttributes())
        {
            CustomAttribute attribute = metadataReader.GetCustomAttribute(handle);
            if (TryGetAttributeConstructorParameterCount(
                metadataReader,
                attribute,
                NeutralResourcesLanguageAttributeName,
                out int parameterCount))
            {
                if (foundNeutralResourcesLanguage)
                {
                    throw new BadImageFormatException(
                        "The assembly contains multiple neutral resources language attributes.");
                }

                foundNeutralResourcesLanguage = true;
                neutralCultureName = ReadNeutralCultureName(metadataReader, attribute, parameterCount);
                continue;
            }

            if (!TryGetAttributeConstructorParameterCount(
                metadataReader,
                attribute,
                SatelliteContractVersionAttributeName,
                out parameterCount))
            {
                continue;
            }

            if (foundSatelliteContractVersion)
            {
                throw new BadImageFormatException(
                    "The assembly contains multiple satellite contract version attributes.");
            }

            foundSatelliteContractVersion = true;
            satelliteContractVersion = ReadSatelliteContractVersion(metadataReader, attribute, parameterCount);
        }

        return new(
            identity,
            $"{identity.Name}.resources.dll",
            neutralCultureName,
            satelliteContractVersion);
    }

    private static string ReadNeutralCultureName(
        MetadataReader metadataReader,
        CustomAttribute attribute,
        int parameterCount)
    {
        if (parameterCount is not 1 and not 2)
        {
            throw new BadImageFormatException(
                "The neutral resources language attribute constructor is invalid.");
        }

        BlobReader value = GetAttributeValueReader(metadataReader, attribute);
        string cultureName = value.ReadSerializedString()
            ?? throw new BadImageFormatException(
                "The neutral resources language attribute culture is null.");

        int fallbackLocation = parameterCount == 2
            ? value.ReadInt32()
            : (int)UltimateResourceFallbackLocation.MainAssembly;

        ReadAndValidateNamedArgumentCount(ref value);

        if (fallbackLocation == (int)UltimateResourceFallbackLocation.Satellite)
        {
            throw new NotSupportedException("Neutral resources stored in a satellite are not supported.");
        }

        if (fallbackLocation != (int)UltimateResourceFallbackLocation.MainAssembly)
        {
            throw new BadImageFormatException(
                "The neutral resources language attribute fallback location is invalid.");
        }

        return cultureName;
    }

    private static Version ReadSatelliteContractVersion(
        MetadataReader metadataReader,
        CustomAttribute attribute,
        int parameterCount)
    {
        if (parameterCount != 1)
        {
            throw new BadImageFormatException(
                "The satellite contract version attribute constructor is invalid.");
        }

        BlobReader value = GetAttributeValueReader(metadataReader, attribute);
        string? versionText = value.ReadSerializedString();
        ReadAndValidateNamedArgumentCount(ref value);
        if (versionText is null || !Version.TryParse(versionText, out Version? version))
        {
            throw new BadImageFormatException("The satellite contract version is invalid.");
        }

        return version;
    }

    private static BlobReader GetAttributeValueReader(
        MetadataReader metadataReader,
        CustomAttribute attribute)
    {
        BlobReader value = metadataReader.GetBlobReader(attribute.Value);
        if (value.ReadUInt16() != 1)
        {
            throw new BadImageFormatException("The custom attribute prolog is invalid.");
        }

        return value;
    }

    private static void ReadAndValidateNamedArgumentCount(ref BlobReader value)
    {
        if (value.ReadUInt16() != 0 || value.RemainingBytes != 0)
        {
            throw new BadImageFormatException("The resource assembly attribute value is invalid.");
        }
    }

    private static bool TryGetAttributeConstructorParameterCount(
        MetadataReader metadataReader,
        CustomAttribute attribute,
        string attributeName,
        out int parameterCount)
    {
        parameterCount = 0;
        if (attribute.Constructor.Kind != HandleKind.MemberReference)
        {
            return false;
        }

        MemberReference constructor = metadataReader.GetMemberReference(
            (MemberReferenceHandle)attribute.Constructor);

        if (!metadataReader.StringComparer.Equals(constructor.Name, ".ctor")
            || constructor.Parent.Kind != HandleKind.TypeReference)
        {
            return false;
        }

        TypeReference type = metadataReader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
        if (!metadataReader.StringComparer.Equals(type.Namespace, ResourcesNamespace)
            || !metadataReader.StringComparer.Equals(type.Name, attributeName))
        {
            return false;
        }

        BlobReader signature = metadataReader.GetBlobReader(constructor.Signature);
        SignatureHeader header = signature.ReadSignatureHeader();
        if (header.IsGeneric)
        {
            signature.ReadCompressedInteger();
        }

        parameterCount = signature.ReadCompressedInteger();
        return true;
    }
}
