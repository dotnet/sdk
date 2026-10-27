// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
#if TARGET_WINDOWS
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.DotNet.Cli.Installer.Windows.Security;
#endif

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate
{
    /// <summary>
    /// Requires a Microsoft-rooted Authenticode signature on a Windows replacement when the installed
    /// executable is Authenticode-signed or admin mode is active. Other unsigned installations and other
    /// platforms rely on the pinned SHA-512 hash alone.
    /// </summary>
    internal static class SelfUpdateSignature
    {
        /// <summary>
        /// Returns whether <see cref="VerifyReplacement"/> requires a Microsoft-rooted Authenticode
        /// signature on the replacement for <paramref name="installedPath"/>. Such a download is not
        /// trusted on its hash alone, so the unsigned-download policy does not apply to it.
        /// </summary>
        [SupportedOSPlatformGuard("windows5.1.2600")]
        public static bool RequiresSignedReplacement(string installedPath, bool? isAdmin = null)
        {
#if TARGET_WINDOWS
            return OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600)
            && ((isAdmin ?? AccessModeResolver.Resolve().IsAdmin) || Signature.IsAuthenticodeSigned(installedPath) == 0);
#else
            return false;
#endif
        }

        public static void VerifyReplacement(string installedPath, string stagedPath, bool? isAdmin = null)
        {
            bool admin = isAdmin ?? AccessModeResolver.Resolve().IsAdmin;
            if (admin && !RequiresSignedReplacement(installedPath, admin))
            {
                throw new DotnetInstallException(DotnetInstallErrorCode.AdminModeSignatureRequired,
                    Microsoft.Dotnet.Installation.Strings.AdminModeSignatureRequired);
            }
#if TARGET_WINDOWS
            if (!RequiresSignedReplacement(installedPath, admin))
            {
                return;
            }

            int status;
            try
            {
                status = Signature.IsAuthenticodeSigned(stagedPath);
                if (status == 0)
                {
                    status = Signature.HasMicrosoftTrustedRoot(stagedPath);
                }
            }
            catch (CryptographicException exception)
            {
                throw CreateFailure(exception.Message, exception, admin);
            }

            if (status != 0)
            {
                throw CreateFailure(Marshal.GetPInvokeErrorMessage(status), innerException: null, admin);
            }
#endif
        }

#if TARGET_WINDOWS
        private static DotnetInstallException CreateFailure(string reason, Exception? innerException, bool isAdmin)
        {
            var message = isAdmin ? Microsoft.Dotnet.Installation.Strings.AdminModeSignatureRequired
                : string.Format(CultureInfo.CurrentCulture, Strings.SelfUpdateSignatureInvalid, reason);
            var errorCode = isAdmin ? DotnetInstallErrorCode.AdminModeSignatureRequired : DotnetInstallErrorCode.SignatureVerificationFailed;
            return innerException is null
                ? new(errorCode, message)
                : new(errorCode, message, innerException);
        }
#endif
    }
}

#if TARGET_WINDOWS
namespace Microsoft.DotNet.Cli
{
    /// <summary>Supplies the one CLI resource used by the shared workload <c>Signature</c> source file.</summary>
    internal static class CliStrings
    {
        public static string UnableToCheckCertificateChainPolicy => Microsoft.DotNet.Tools.Bootstrapper.Strings.SelfUpdateCertificateChainPolicyUnavailable;
    }
}
#endif
