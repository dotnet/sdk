// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Cryptography;
using Windows.Win32.Security.WinTrust;
using static Windows.Win32.PInvoke;

namespace Microsoft.DotNet.Installer.Windows.Security
{
    /// <summary>
    /// Defines <see cref="X509Certificate"/> extension methods.
    /// </summary>
#if NETCOREAPP
    [SupportedOSPlatform("windows5.1.2600")]
#endif
    public static class X509CertificateExtensions
    {
        /// <summary>
        /// Extended key usage OID for id-kp-codeSigning.
        /// </summary>
        /// <remarks>Refer to <see href="https://datatracker.ietf.org/doc/html/rfc5280#section-4.2.1.12">RFC5280</see> for additional information.</remarks>
        public static readonly Oid ekuCodeSigning = new Oid("1.3.6.1.5.5.7.3.3");

        /// <summary>
        /// Determines whether the certificate is intended for code signing. 
        /// </summary>
        /// <returns><see langword="true"/> if the certificate is intended to be used for code signing; otherwise, <see langword="false"/>.</returns>
        public static bool IsIntendedForCodeSigning(this X509Certificate certificate)
        {
            using X509Chain chain = new();
            chain.ChainPolicy.ApplicationPolicy.Add(ekuCodeSigning);
            return chain.Build(new X509Certificate2(certificate));
        }

        /// <summary>
        /// Verifies that the root element in a certificate chain contains a Microsoft root public key.
        /// </summary>
        /// <param name="certificate">The certificate to verify.</param>
        /// <returns><see langword="true"/> if the certificate has a trusted Microsoft root; otherwise, <see langword="false"/>.</returns>
        public static bool HasMicrosoftTrustedRoot(this X509Certificate certificate)
        {
            unsafe
            {
                CERT_CHAIN_POLICY_PARA policyCriteria = default;
                CERT_CHAIN_POLICY_STATUS policyStatus = default;

                using X509Chain chain = new();
                bool buildResult = chain.Build(new X509Certificate2(certificate));
                policyCriteria.cbSize = (uint)sizeof(CERT_CHAIN_POLICY_PARA);
                policyCriteria.dwFlags = (CERT_CHAIN_POLICY_FLAGS)MICROSOFT_ROOT_CERT_CHAIN_POLICY_CHECK_APPLICATION_ROOT_FLAG;

                return CertVerifyCertificateChainPolicy(CERT_CHAIN_POLICY_MICROSOFT_ROOT, (CERT_CHAIN_CONTEXT*)chain.ChainContext, &policyCriteria, &policyStatus) &&
                    policyStatus.dwError == 0;
            }
        }
    }
}
