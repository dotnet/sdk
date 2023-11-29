// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
#if !DOT_NET_BUILD_FROM_SOURCE
using Microsoft.DotNet.Installer.Windows.Security;
#endif
using Microsoft.Win32;

namespace Microsoft.DotNet.Workloads.Workload
{
    internal static class SignCheck
    {
        private static readonly string s_dotnet = Assembly.GetExecutingAssembly().Location;

        /// <summary>
        /// Determines whether dotnet.dll is signed.
        /// </summary>
        /// <returns><see langword="true"/> if dotnet is signed; otherwise, <see langword="false"/>.</returns>
        public static bool IsDotNetSigned()
        {
            if (OperatingSystem.IsWindows())
            {
#if !DOT_NET_BUILD_FROM_SOURCE
                // API is only available on XP and Server 2003 or later versions. .NET requires Win7 minimum.
#pragma warning disable CA1416
                // We don't care about trust in this case, only whether or not the file has a signatue
                return Signature.IsAuthenticodeSigned(s_dotnet, AllowOnlineRevocationChecks()) == 0;
#pragma warning restore CA1416
#endif
            }

            return false;
        }

        /// <summary>
        /// Determines whether revocation checks can go online when verifying signatures for workloads.
        /// </summary>
        /// <returns><see langword="true"/> if the policy key is absent or set to a non-zero value; <see langword="false"/> if the policy key is set to 0.</returns>
        public static bool AllowOnlineRevocationChecks()
        {
            if (OperatingSystem.IsWindows())
            {
                using RegistryKey policyKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\dotnet\Workloads");

                return ((int?)policyKey?.GetValue("AllowOnlineRevocationChecks") ?? 1) != 0;
            }

            return true;
        }

        /// <summary>
        /// Determines whether the global policy to enforce signature checks for workloads is set.
        /// </summary>
        /// <returns><see langword="true"/> if the policy is set; <see langword="false"/> otherwise.</returns>
        public static bool IsWorkloadSignVerificationPolicySet()
        {
            if (OperatingSystem.IsWindows())
            {
                using RegistryKey policyKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\dotnet\Workloads");

                return ((int?)policyKey?.GetValue("VerifySignatures") ?? 0) != 0;
            }

            return false;
        }


    }
}
