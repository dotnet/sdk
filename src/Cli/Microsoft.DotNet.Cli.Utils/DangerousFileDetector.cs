// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if TARGET_WINDOWS
using System.Runtime.Versioning;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.Urlmon;
#endif

namespace Microsoft.DotNet.Cli.Utils;

internal class DangerousFileDetector : IDangerousFileDetector
{
    public bool IsDangerous(string filePath)
    {
#if TARGET_WINDOWS
        return InternetSecurity.IsDangerous(filePath);
#else
        return false;
#endif
    }

#if TARGET_WINDOWS
    private static class InternetSecurity
    {
        private const string CLSID_InternetSecurityManager = "7b8a2d94-0ac9-11d1-896c-00c04fb6bfc4";
        private const uint ZoneLocalMachine = 0;
        private const uint ZoneIntranet = 1;
        private const uint ZoneTrusted = 2;
        private const uint ZoneInternet = 3;
        private const uint ZoneUntrusted = 4;
        private const int REGDB_E_CLASSNOTREG = unchecked((int)0x80040154);

        private static bool s_attemptedLoad;
        private static unsafe IInternetSecurityManager* s_securityManager = null;

        [SupportedOSPlatform("windows")]
        public static unsafe bool IsDangerous(string filename)
        {
            if (!s_attemptedLoad)
            {
                s_attemptedLoad = true;

                Guid classId = CLSID.InternetSecurityManager;
                Guid iid = IID.Get<IInternetSecurityManager>();
                IInternetSecurityManager* securityManager;

                // Use CoCreateInstance rather than creating the object directly from its class factory
                // (CoGetClassObject + IClassFactory::CreateInstance). InternetSecurityManager is registered
                // with the apartment threading model, and CoCreateInstance honors that model: when called from
                // an MTA thread (such as the xUnit test runner) it hosts the object in an STA and returns a
                // marshaled proxy. Creating the object directly in the calling MTA apartment leaves
                // MapUrlToZone unable to read the Zone.Identifier (Mark of the Web) stream, so it reports the
                // wrong zone and downloaded files are not detected as dangerous.
                HRESULT result = PInvoke.CoCreateInstance(
                    &classId,
                    null,
                    CLSCTX.CLSCTX_INPROC_SERVER,
                    &iid,
                    (void**)&securityManager);

                if (result.Succeeded)
                {
                    s_securityManager = securityManager;
                }
                else if (result != HRESULT.REGDB_E_CLASSNOTREG)
                {
                    // When the COM is missing (Class not registered error), it is in a locked down
                    // version like Nano Server.
                    result.ThrowOnFailure();
                }
            }

            if (s_securityManager is null)
            {
                return false;
            }

            // First check the zone, if they are not an untrusted zone, they aren't dangerous.
            // Pass dwFlags as 0 (not MUTZ_ISFILE) so that MapUrlToZone reads the Zone.Identifier
            // alternate data stream (Mark of the Web). MUTZ_ISFILE would map the zone based solely
            // on the file location and ignore the Zone.Identifier stream.
            filename = Path.GetFullPath(filename);
            HRESULT hr = s_securityManager->MapUrlToZone(filename, out uint zone, 0);

            if (hr.Failed || zone < (uint)URLZONE.URLZONE_INTERNET)
            {
                return false;
            }

            // By default all file types that get here are considered dangerous
            return true;
        }
    }
#endif
}
