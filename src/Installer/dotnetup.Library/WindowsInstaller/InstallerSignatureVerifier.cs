// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
#if TARGET_WINDOWS
using System.Security.Cryptography;
using Microsoft.DotNet.Cli.Installer.Windows.Security;
#endif

namespace Microsoft.DotNet.Tools.Bootstrapper.WindowsInstaller;

/// <summary>The outcome of verifying an installer's Authenticode signature.</summary>
internal readonly record struct InstallerSignatureResult(bool IsTrusted, string? FailureReason)
{
    public static InstallerSignatureResult Trusted { get; } = new(true, null);

    public static InstallerSignatureResult Untrusted(string reason) => new(false, reason);
}

/// <summary>Verifies the Authenticode signature of a downloaded installer before it is run.</summary>
internal interface IInstallerSignatureVerifier
{
    InstallerSignatureResult Verify(string installerPath);
}

/// <summary>
/// Requires a valid Authenticode signature that chains to a Microsoft trusted root, using the same
/// checks as workload MSI installs.
/// </summary>
/// <remarks>The platform version matches the shared workload <c>Signature</c> implementation.</remarks>
[SupportedOSPlatform("windows5.1.2600")]
internal sealed class AuthenticodeInstallerSignatureVerifier : IInstallerSignatureVerifier
{
    public InstallerSignatureResult Verify(string installerPath)
    {
#if TARGET_WINDOWS
        try
        {
            int status;
            bool trusted = (status = Signature.IsAuthenticodeSigned(installerPath)) == 0
                && (status = Signature.HasMicrosoftTrustedRoot(installerPath)) == 0;

            return trusted
                ? InstallerSignatureResult.Trusted
                : InstallerSignatureResult.Untrusted(Marshal.GetPInvokeErrorMessage(status));
        }
        catch (CryptographicException ex)
        {
            return InstallerSignatureResult.Untrusted(ex.Message);
        }
#else
        return UnsupportedPlatformInstallerSignatureVerifier.Instance.Verify(installerPath);
#endif
    }
}

/// <summary>Rejects every installer on platforms where Authenticode verification is unavailable.</summary>
internal sealed class UnsupportedPlatformInstallerSignatureVerifier : IInstallerSignatureVerifier
{
    public static UnsupportedPlatformInstallerSignatureVerifier Instance { get; } = new();

    private UnsupportedPlatformInstallerSignatureVerifier()
    {
    }

    public InstallerSignatureResult Verify(string installerPath) =>
        InstallerSignatureResult.Untrusted("Authenticode verification is only supported on Windows.");
}
