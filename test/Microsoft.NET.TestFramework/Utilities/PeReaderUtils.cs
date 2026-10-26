// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.NET.TestFramework.Utilities
{
    public static class PeReaderUtils
    {
        public static bool IsCrossgened(this PEReader peReader)
        {
            const int CROSSGEN_FLAG = 4;
            bool isCrossgened = false;

            if (peReader.HasMetadata && peReader.PEHeaders.CorHeader is not null)
            {
                // 4 is the magic numbers that is set in the CLR header's flags when crossgened.
                isCrossgened = ((int)peReader.PEHeaders.CorHeader.Flags & CROSSGEN_FLAG) == CROSSGEN_FLAG;
            }

            return isCrossgened;
        }

        public static bool IsCetCompatible(string filePath)
        {
            using (PEReader reader = new PEReader(new FileStream(filePath, FileMode.Open, FileAccess.Read)))
            {
                foreach (DebugDirectoryEntry entry in reader.ReadDebugDirectory())
                {
                    // https://learn.microsoft.com/windows/win32/debug/pe-format#debug-type
                    const int IMAGE_DEBUG_TYPE_EX_DLLCHARACTERISTICS = 20;
                    if ((int)entry.Type != IMAGE_DEBUG_TYPE_EX_DLLCHARACTERISTICS)
                        continue;

                    // Get the extended DLL characteristics debug directory entry
                    PEMemoryBlock data = reader.GetSectionData(entry.DataRelativeVirtualAddress);
                    ushort dllCharacteristics = data.GetReader().ReadUInt16();

                    // Check for the CET compat bit
                    // https://learn.microsoft.com/windows/win32/debug/pe-format#extended-dll-characteristics
                    const ushort IMAGE_DLLCHARACTERISTICS_EX_CET_COMPAT = 0x1;
                    return (dllCharacteristics & IMAGE_DLLCHARACTERISTICS_EX_CET_COMPAT) != 0;
                }

                // Not marked compatible - no debug directory entry for extended DLL characteristics
                return false;
            }
        }

        public static string? GetAssemblyAttributeValue(string assemblyPath, string attributeName)
        {
            if (!File.Exists(assemblyPath))
            {
                return null;
            }

            using (var stream = File.OpenRead(assemblyPath))
            using (var peReader = new PEReader(stream))
            {
                if (!peReader.HasMetadata)
                {
                    return null;
                }

                var mdReader = peReader.GetMetadataReader();
                var attrs = mdReader.GetAssemblyDefinition().GetCustomAttributes()
                    .Select(ah => mdReader.GetCustomAttribute(ah));

                foreach (var attr in attrs)
                {
                    var ctorHandle = attr.Constructor;
                    if (ctorHandle.Kind != HandleKind.MemberReference)
                    {
                        continue;
                    }

                    var container = mdReader.GetMemberReference((MemberReferenceHandle)ctorHandle).Parent;
                    var name = mdReader.GetTypeReference((TypeReferenceHandle)container).Name;
                    if (!string.Equals(mdReader.GetString(name), attributeName))
                    {
                        continue;
                    }

                    var arguments = GetFixedStringArguments(mdReader, attr);
                    if (arguments.Count == 1)
                    {
                        return arguments[0];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the fixed (required) string arguments of a custom attribute.
        /// Only attributes that have only fixed string arguments.
        /// </summary>
        private static List<string> GetFixedStringArguments(MetadataReader reader, CustomAttribute attribute)
        {
            // TODO: Nick Guerrera (Nick.Guerrera@microsoft.com) hacked this method for temporary use.
            // There is a blob decoder feature in progress but it won't ship in time for our milestone.
            // Replace this method with the blob decoder feature when later it is available.

            var signature = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Signature;
            var signatureReader = reader.GetBlobReader(signature);
            var valueReader = reader.GetBlobReader(attribute.Value);
            var arguments = new List<string>();

            var prolog = valueReader.ReadUInt16();
            if (prolog != 1)
            {
                // Invalid custom attribute prolog
                return arguments;
            }

            var header = signatureReader.ReadSignatureHeader();
            if (header.Kind != SignatureKind.Method || header.IsGeneric)
            {
                // Invalid custom attribute constructor signature
                return arguments;
            }

            int parameterCount;
            if (!signatureReader.TryReadCompressedInteger(out parameterCount))
            {
                // Invalid custom attribute constructor signature
                return arguments;
            }

            var returnType = signatureReader.ReadSignatureTypeCode();
            if (returnType != SignatureTypeCode.Void)
            {
                // Invalid custom attribute constructor signature
                return arguments;
            }

            for (int i = 0; i < parameterCount; i++)
            {
                var signatureTypeCode = signatureReader.ReadSignatureTypeCode();
                if (signatureTypeCode == SignatureTypeCode.String)
                {
                    // Custom attribute constructor must take only strings
                    arguments.Add(valueReader.ReadSerializedString() ?? string.Empty);
                }
            }

            return arguments;
        }
    }
}
