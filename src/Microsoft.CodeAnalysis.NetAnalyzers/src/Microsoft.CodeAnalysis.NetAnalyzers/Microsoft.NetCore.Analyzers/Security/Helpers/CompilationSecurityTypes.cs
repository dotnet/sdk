// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;

namespace Microsoft.NetCore.Analyzers.Security.Helpers
{
    public class CompilationSecurityTypes(Compilation compilation)
    {
        // Some of these types may only exist in .NET Framework and not in .NET Core, but that's okay, we'll look anyway.

        public INamedTypeSymbol? MD5 { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographyMD5);
        public INamedTypeSymbol? SHA1 { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographySHA1);
        public INamedTypeSymbol? HMACSHA1 { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographyHMACSHA1);
        public INamedTypeSymbol? DES { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographyDES);
        public INamedTypeSymbol? DSA { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographyDSA);
        public INamedTypeSymbol? DSASignatureFormatter { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographyDSASignatureFormatter);
        public INamedTypeSymbol? HMACMD5 { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographyHMACMD5);
        public INamedTypeSymbol? RC2 { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographyRC2);
        public INamedTypeSymbol? TripleDES { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographyTripleDES);
        public INamedTypeSymbol? RIPEMD160 { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographyRIPEMD160);
        public INamedTypeSymbol? HMACRIPEMD160 { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographyHMACRIPEMD160);
    }
}
