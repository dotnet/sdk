// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.DotNet.Installer.Windows.Security;
using Microsoft.Win32;

namespace Microsoft.DotNet.Workloads.Workload
{
    internal static class SignCheck
    {
        private static readonly string s_dotnet = Assembly.GetExecutingAssembly().Location;

        /// <summary>
        /// Determines whether dotnet is signed.
        /// </summary>
        /// <returns><see langword="true"/> if dotnet is signed; <see langword="false"/> otherwise.</returns>
        public static bool IsDotNetSigned() => IsSigned(s_dotnet);

        /// <summary>
        /// Determines whether the specified file is signed by a trusted organization.
        /// </summary>
        /// <returns><see langword="true"/> if file is signed; <see langword="false"/> otherwise.</returns>
        internal static bool IsSigned(string path)
        {
            if (OperatingSystem.IsWindows())
            {
#if !DOT_NET_BUILD_FROM_SOURCE
                // API is only available on XP and Server 2003 or later versions. .NET requires Win7 minimum.
#pragma warning disable CA1416
                if (AuthentiCode.IsSigned(path, IsCacheOnlyRevocationChecksPolicySet()) == 0)
                {
                    X509Certificate certificate = X509Certificate.CreateFromSignedFile(path);

                    return certificate.IsIntendedForCodeSigning() && certificate.HasMicrosoftTrustedRoot();
                }
#pragma warning restore CA1416
#endif
            }

            return false;
        }

        /// <summary>
        /// Determines whether the global policy to limit revocation checks to cached URLs for workloads is set.
        /// </summary>
        /// <returns><see langword="true"/> if the policy is set; <see langword="false"/> otherwise.</returns>
        public static bool IsCacheOnlyRevocationChecksPolicySet()
        {
            if (OperatingSystem.IsWindows())
            {
                using RegistryKey policyKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\dotnet\Workloads");

                return ((int?)policyKey?.GetValue("CacheOnlyRevocationChecks") ?? 0) != 0;
            }

            return false;
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
