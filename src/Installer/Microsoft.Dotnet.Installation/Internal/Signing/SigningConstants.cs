// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal.Signing;

/// <summary>
/// Shared constants for CMS signature verification.
/// </summary>
internal static class SigningConstants
{
    /// <summary>
    /// X.500 attribute type OIDs used in Distinguished Name comparisons.
    /// Defined in RFC 5280 §4.1.2.6 and the ITU-T X.520 attribute type arc (2.5.4.*).
    /// https://datatracker.ietf.org/doc/html/rfc5280#section-4.1.2.6
    /// </summary>
    internal static class DnOids
    {
        public const string CommonName          = "2.5.4.3";   // id-at-commonName
        public const string OrganizationalUnit  = "2.5.4.11";  // id-at-organizationalUnitName
        public const string Organization        = "2.5.4.10";  // id-at-organizationName
        public const string Locality            = "2.5.4.7";   // id-at-localityName
        public const string StateOrProvince     = "2.5.4.8";   // id-at-stateOrProvinceName
        public const string Country             = "2.5.4.6";   // id-at-countryName
    }

    /// <summary>
    /// Well-known Distinguished Name attribute values pinned by the verifier.
    /// Centralised here so a certificate renewal that changes an issuer name
    /// or organization string is updated in exactly one place.
    /// </summary>
    internal static class DnValues
    {
        public const string Microsoft       = "Microsoft Corporation";
        public const string DigiCertOrg     = "DigiCert, Inc.";
        public const string UnitedStates    = "US";
    }
}
