// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.Urlmon;

namespace Microsoft.DotNet.Cli.Utils;

internal class DangerousFileDetector : IDangerousFileDetector
{
    public bool IsDangerous(string filePath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        return InternetSecurity.IsDangerous(filePath);
    }

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
        private static ComClassFactory? s_classFactory = null;

        [SupportedOSPlatform("windows")]
        public static unsafe bool IsDangerous(string filename)
        {
            if (!s_attemptedLoad)
            {
                s_attemptedLoad = true;
                if (!ComClassFactory.TryCreate(CLSID.InternetSecurityManager, out s_classFactory, out HRESULT result))
                {
                    // When the COM is missing(Class not registered error), it is in a locked down
                    // version like Nano Server

                    if (result != HRESULT.REGDB_E_CLASSNOTREG)
                    {
                        result.ThrowOnFailure();
                    }
                }
            }

            if (s_classFactory is not { } factory)
            {
                return false;
            }

            using var securityManager = factory.TryCreateInstance<IInternetSecurityManager>(out HRESULT hr);
            if (hr.Failed)
            {
                return false;
            }

            // First check the zone, if they are not an untrusted zone, they aren't dangerous
            filename = Path.GetFullPath(filename);
            hr = securityManager.Pointer->MapUrlToZone(filename, out uint zone, PInvoke.MUTZ_ISFILE);

            if (zone < (uint)URLZONE.URLZONE_INTERNET)
            {
                return false;
            }

            // By default all file types that get here are considered dangerous
            return true;
        }
    }
}
