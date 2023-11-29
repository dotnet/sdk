// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using Microsoft.DotNet.Workloads.Workload;
using Windows.Win32.Foundation;
using Windows.Win32.Security.WinTrust;
using static Windows.Win32.PInvoke;
using Windows.Win32.Security.Cryptography;
using System.Security.Cryptography;

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
        /// Extended key usage OID for id-kp-codeSigning.
        /// </summary>
        private static readonly Oid s_EkuCodeSigningOid = new Oid("1.3.6.1.5.5.7.3.3");

        /// <summary>
        /// Tries to verify that the specified file contains a valid Authenticode signature associated with
        /// a trusted Microsoft root certificate.
        /// </summary>
        /// <param name="path">The path of the file to verify.</param>
        /// <param name="result">If verification fails, the result may contain a status or error code. A detailed error message may be obtained
        /// by calling <see cref="Marshal.GetPInvokeErrorMessage(int)"/>.</param>
        /// <returns><see langword="true"/> if the file has a valid Microsoft Authenticode signature; otherwise, <see langword="false"/>.</returns>
        public static bool TryVerifyMicrosoftAutheticodeSigned(string path, out int result)
        {
            result = IsAuthenticodeSigned(path, SignCheck.AllowOnlineRevocationChecks());

            if (result != 0)
            {
                return false;
            }

            // Verify the certificate's EKU to ensure it's intended for code signing.
            using X509Chain chain = new();
            using X509Certificate2 certificate = new(path);

            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;
            chain.ChainPolicy.ApplicationPolicy.Add(s_EkuCodeSigningOid);

            if (!chain.Build(certificate) || DateTime.Now < certificate.NotBefore)
            {
                return false;
            }

            // Verify that the root certificate is a trusted Microsoft root. X509 APIs do not support the Microsoft root chain policy flag
            // so we have to call CertVerifyCertificateChainPolicy directly. Note: we're relying on the certificate chain context
            // previously created when chain.Build() was called.
            CERT_CHAIN_POLICY_PARA policyCriteria = default;
            CERT_CHAIN_POLICY_STATUS policyStatus = default;

            unsafe
            {
                policyCriteria.cbSize = (uint)sizeof(CERT_CHAIN_POLICY_PARA);
                policyCriteria.dwFlags = (CERT_CHAIN_POLICY_FLAGS)MICROSOFT_ROOT_CERT_CHAIN_POLICY_CHECK_APPLICATION_ROOT_FLAG;

                // The result will always be true if the chain policy could be checked, regardless of the outcome. CertVerifyCertificateChainPolicy
                // only returns false if the policy could not be checked. 
                bool policyChecked = CertVerifyCertificateChainPolicy(CERT_CHAIN_POLICY_MICROSOFT_ROOT, (CERT_CHAIN_CONTEXT*)chain.ChainContext, &policyCriteria, &policyStatus);
                result = (int)policyStatus.dwError;

                return policyChecked && result == 0;
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
        public static unsafe int IsAuthenticodeSigned(string path, bool allowOnlineRevocationChecks = true)
        {
            WINTRUST_FILE_INFO* fileInfo = stackalloc WINTRUST_FILE_INFO[1];
            WINTRUST_DATA* trustData = stackalloc WINTRUST_DATA[1];

            fixed (char* p = Path.GetFullPath(path))
            {
                fileInfo[0].pcwszFilePath = p;
                fileInfo[0].cbStruct = (uint)sizeof(WINTRUST_FILE_INFO);
                fileInfo[0].hFile = (HANDLE)IntPtr.Zero;
                fileInfo[0].pgKnownSubject = null;

                Guid policyGuid = WINTRUST_ACTION_GENERIC_VERIFY_V2;

                trustData[0].cbStruct = (uint)sizeof(WINTRUST_DATA);
                trustData[0].pPolicyCallbackData = null;
                trustData[0].pSIPClientData = null;
                trustData[0].dwUIChoice = WINTRUST_DATA_UICHOICE.WTD_UI_NONE;
                trustData[0].fdwRevocationChecks = WINTRUST_DATA_REVOCATION_CHECKS.WTD_REVOKE_WHOLECHAIN;
                trustData[0].dwUnionChoice = WINTRUST_DATA_UNION_CHOICE.WTD_CHOICE_FILE;
                trustData[0].dwStateAction = WINTRUST_DATA_STATE_ACTION.WTD_STATEACTION_VERIFY;
                trustData[0].hWVTStateData = (HANDLE)IntPtr.Zero;
                trustData[0].pwszURLReference = null;
                trustData[0].dwProvFlags = WINTRUST_DATA_PROVIDER_FLAGS.WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT;

                if (!allowOnlineRevocationChecks)
                {
                    trustData[0].dwProvFlags |= WINTRUST_DATA_PROVIDER_FLAGS.WTD_CACHE_ONLY_URL_RETRIEVAL;
                }

                trustData[0].dwUIContext = 0;
                trustData[0].Anonymous.pFile = fileInfo;

                int lstatus = WinVerifyTrust((HWND)IntPtr.Zero, ref policyGuid, trustData);

                // Release the hWVTStateData handle, but return the original status.
                trustData[0].dwStateAction = WINTRUST_DATA_STATE_ACTION.WTD_STATEACTION_CLOSE;
                _ = WinVerifyTrust((HWND)IntPtr.Zero, ref policyGuid, trustData);

                return lstatus;
            }
        }
    }
}
