// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Windows.Win32.Foundation;
using Windows.Win32.Security.WinTrust;
using static Windows.Win32.PInvoke;

namespace Microsoft.DotNet.Installer.Windows.Security
{
    /// <summary>
    /// Contains various utilities methods around verifying AuthentiCode signatures on Windows.
    /// </summary>
#if NETCOREAPP
    [SupportedOSPlatform("windows5.1.2600")]
#endif
    internal static class AuthentiCode
    {
        /// <summary>
        /// Verifies that the specified file is Authenticode signed.
        /// </summary>
        /// <param name="path">The path of the file to verify.</param>
        /// <param name="cacheOnlyRevocationChecks">Prevents revocation checks over the network when set to <see langword="true"/>.</param>
        /// <returns>0 if successful.</returns>
        public static unsafe int IsSigned(string path, bool cacheOnlyRevocationChecks = false)
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

                if (cacheOnlyRevocationChecks)
                {
                    trustData[0].dwProvFlags |= WINTRUST_DATA_PROVIDER_FLAGS.WTD_CACHE_ONLY_URL_RETRIEVAL;
                }

                trustData[0].dwUIContext = 0;
                trustData[0].Anonymous.pFile = fileInfo;

                return WinVerifyTrust((HWND)IntPtr.Zero, ref policyGuid, trustData);                
            }
        }        
    }
}
