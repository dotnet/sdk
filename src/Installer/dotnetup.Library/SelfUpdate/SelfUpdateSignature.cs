// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if TARGET_WINDOWS
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.DotNet.Cli.Installer.Windows.Security;
#endif

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate
{
    /// <summary>
    /// Requires a Microsoft-rooted Authenticode signature on a Windows replacement when the installed
    /// executable is Authenticode-signed. Unsigned installations, such as local builds, and other
    /// platforms rely on the pinned SHA-512 hash alone.
    /// </summary>
    internal static class SelfUpdateSignature
    {
        public static void VerifyReplacement(string installedPath, string stagedPath)
        {
#if TARGET_WINDOWS
            if (!OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600) || Signature.IsAuthenticodeSigned(installedPath) != 0)
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
                throw CreateFailure(exception.Message, exception);
            }

            if (status != 0)
            {
                throw CreateFailure(Marshal.GetPInvokeErrorMessage(status), innerException: null);
            }
#endif
        }

#if TARGET_WINDOWS
        private static DotnetInstallException CreateFailure(string reason, Exception? innerException)
        {
            var message = string.Format(CultureInfo.CurrentCulture, Strings.SelfUpdateSignatureInvalid, reason);
            return innerException is null
                ? new(DotnetInstallErrorCode.SignatureVerificationFailed, message)
                : new(DotnetInstallErrorCode.SignatureVerificationFailed, message, innerException);
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
