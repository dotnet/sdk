// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Cryptography;
using Windows.Win32.Security.WinTrust;
using static Windows.Win32.PInvoke;

namespace Microsoft.DotNet.Installer.Windows.Security
{
    /// <summary>
    /// Contains methods for verifying Authenticode signatures on Windows.
    /// </summary>
#if NETCOREAPP
    [SupportedOSPlatform("windows5.1.2600")]
#endif
    internal static class Signature
    {
        /// <summary>
        /// Verifies that the certificate used to sign the specified file terminates in a trusted Microsoft root certificate. The policy check is performed against
        /// the first simple chain. 
        /// </summary>
        /// <param name="path">The path of the file to verify.</param>
        /// <returns>0 if the policy could be checked.<see cref="Marshal.GetPInvokeErrorMessage(int)"/> can be called to obtain more detail about the failure.</returns>
        /// <exception cref="CryptographicException"/>
        /// <remarks>This method does not perform any other chain validation like revocation checks, timestamping, etc.</remarks>
        internal static unsafe int HasMicrosoftTrustedRoot(string path)
        {
            var certContentType = X509Certificate2.GetCertContentType(path);
            if (certContentType != X509ContentType.Authenticode)
            {
                throw new CryptographicException($"Unexpected certificate content type, got '{certContentType}' instead of Authenticode.");
            }

            // Create an X509Certificate2 instance so we can access the certificate context and create a chain context.
#pragma warning disable SYSLIB0057 // we need Authenticode support which isn't available from X509CertificateLoader
            using X509Certificate2 certificate = new(path);
#pragma warning restore SYSLIB0057

            // We don't use X509Chain because it doesn't support verifying the specific policy and because we defer
            // that to the WinTrust provider as it performs timestamp and revocation checks.
            HCERTCHAINENGINE HCCE_LOCAL_MACHINE = (HCERTCHAINENGINE)0x01;
            CERT_CHAIN_PARA pChainPara = default;
            CERT_CHAIN_CONTEXT* pChainContext = default;
            CERT_CONTEXT* pCertContext = (CERT_CONTEXT*)certificate.Handle;
            uint dwFlags = 0;

            try
            {
                if (!CertGetCertificateChain(HCCE_LOCAL_MACHINE, pCertContext, null, default, &pChainPara, dwFlags, null, &pChainContext))
                {
                    throw new CryptographicException(Marshal.GetPInvokeErrorMessage(Marshal.GetLastWin32Error()));
                }

                CERT_CHAIN_POLICY_PARA policyCriteria = default;
                CERT_CHAIN_POLICY_STATUS policyStatus = default;

                policyCriteria.cbSize = (uint)sizeof(CERT_CHAIN_POLICY_PARA);
                policyCriteria.dwFlags = (CERT_CHAIN_POLICY_FLAGS)MICROSOFT_ROOT_CERT_CHAIN_POLICY_CHECK_APPLICATION_ROOT_FLAG;

                if (!CertVerifyCertificateChainPolicy(CERT_CHAIN_POLICY_MICROSOFT_ROOT, pChainContext, &policyCriteria, &policyStatus))
                {
                    throw new CryptographicException(string.Format(LocalizableStrings.UnableToCheckCertificateChainPolicy, nameof(CERT_CHAIN_POLICY_MICROSOFT_ROOT)));
                }

                return (int)policyStatus.dwError;
            }
            finally
            {
                CertFreeCertificateChain(pChainContext);
            }
        }

        /// <summary>
        /// Verifies that the specified file is Authenticode signed.
        /// </summary>
        /// <param name="path">The path of the file to verify.</param>
        /// <param name="allowOnlineRevocationChecks">Allow revocation checks to go online. When set to <see langword="false"/>, the cached certificate revocation list is used instead.</param>
        /// <returns>0 if successful. <see cref="Marshal.GetPInvokeErrorMessage(int)"/> can be called to obtain more detail about the failure.</returns>
        /// <remarks>See this <see href="https://learn.microsoft.com/en-us/windows/win32/seccrypto/example-c-program--verifying-the-signature-of-a-pe-file">example</see> for more information.
        /// A valid Authenticode signature does not establish trust. For example, Microsoft SHA1 signatures will return a positive result, even though their
        /// root certificates are no longer trusted. This simply verifies that the Authenticode signature is valid.
        /// </remarks>
        internal static unsafe int IsAuthenticodeSigned(string path, bool allowOnlineRevocationChecks = true)
        {
            fixed (char* p = Path.GetFullPath(path))
            {
                WINTRUST_FILE_INFO fileInfo = new()
                {
                    pcwszFilePath = p,
                    cbStruct = (uint)sizeof(WINTRUST_FILE_INFO),
                };

                Guid policyGuid = WINTRUST_ACTION_GENERIC_VERIFY_V2;

                WINTRUST_DATA trustData = new()
                {
                    cbStruct = (uint)sizeof(WINTRUST_DATA),
                    dwUIChoice = WINTRUST_DATA_UICHOICE.WTD_UI_NONE,
                    fdwRevocationChecks = WINTRUST_DATA_REVOCATION_CHECKS.WTD_REVOKE_WHOLECHAIN,
                    dwUnionChoice = WINTRUST_DATA_UNION_CHOICE.WTD_CHOICE_FILE,
                    dwStateAction = WINTRUST_DATA_STATE_ACTION.WTD_STATEACTION_VERIFY,
                    dwProvFlags = WINTRUST_DATA_PROVIDER_FLAGS.WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT,
                };

                if (!allowOnlineRevocationChecks)
                {
                    trustData.dwProvFlags |= WINTRUST_DATA_PROVIDER_FLAGS.WTD_CACHE_ONLY_URL_RETRIEVAL;
                }

                trustData.Anonymous.pFile = &fileInfo;

                int lstatus = WinVerifyTrust((HWND)IntPtr.Zero, ref policyGuid, &trustData);

                // Release the hWVTStateData handle, but return the original status.
                trustData.dwStateAction = WINTRUST_DATA_STATE_ACTION.WTD_STATEACTION_CLOSE;
                _ = WinVerifyTrust((HWND)IntPtr.Zero, ref policyGuid, &trustData);

                return lstatus;
            }
        }
    }
}
