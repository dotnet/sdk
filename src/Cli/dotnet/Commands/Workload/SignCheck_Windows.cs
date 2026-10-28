// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Installer.Windows.Security;
using Microsoft.Win32;

namespace Microsoft.DotNet.Cli.Commands.Workload;

/// <summary>
/// Windows-only helper for checking whether the dotnet host is Authenticode-signed
/// and for reading workload-related signature verification policies from the registry.
/// </summary>
/// <remarks>
/// <para>
/// This class is used at the very start of the signature verification decision pipeline.
/// <see cref="IsDotNetSigned"/> answers the question: "Was this SDK installation itself signed?"
/// If the host is unsigned (e.g., a locally-built SDK), downstream signature checks are skipped
/// because the installation is already untrusted.
/// </para>
/// <para>
/// The registry-based policies (<see cref="AllowOnlineRevocationChecks"/> and
/// <see cref="IsWorkloadSignVerificationPolicySet"/>) allow administrators to control
/// behavior via Group Policy at <c>HKLM\SOFTWARE\Policies\Microsoft\dotnet\Workloads</c>.
/// </para>
/// </remarks>
internal static class SignCheck
{
    private const string OnlineRevocationCheckPolicyKeyName = "AllowOnlineRevocationChecks";
    private const string VerifySignaturesPolicyKeyName = "VerifySignatures";
    private const string WorkloadPolicyKey = @"SOFTWARE\Policies\Microsoft\dotnet\Workloads";

    private static readonly string? s_dotnet = Environment.ProcessPath;

    /// <summary>
    /// Determines whether the dotnet host binary (<c>dotnet.exe</c>) has a valid Authenticode signature.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is intended as a "trust gate" — if the host itself is unsigned, the calling code
    /// skips all downstream signature verification (NuGet package signatures and MSI Authenticode).
    /// </para>
    /// <para>
    /// Note: this checks that a valid Authenticode signature <b>exists</b>. It does <b>not</b>
    /// verify that the signer is Microsoft or that the certificate terminates in a trusted root.
    /// That level of trust verification is performed separately for MSI payloads by
    /// <see cref="Installer.Windows.Security.Signature.HasMicrosoftTrustedRoot"/>.
    /// </para>
    /// </remarks>
    /// <returns><see langword="true"/> if the dotnet host is Authenticode-signed; <see langword="false"/> otherwise.</returns>
    public static bool IsDotNetSigned()
    {
        Debug.Assert(s_dotnet is not null, "Environment.ProcessPath should not be null when running in the dotnet host.");

        if (s_dotnet is null)
        {
            return false;
        }

        // WinVerifyTrust is available on XP/Server 2003+. .NET requires Win7 minimum.
#pragma warning disable CA1416
        // We only check whether an Authenticode signature exists — we do NOT check trust or Microsoft root.
        // This is purely to decide whether to enable downstream verification for workload packages and MSIs.
        return Signature.IsAuthenticodeSigned(s_dotnet, AllowOnlineRevocationChecks()) == 0;
#pragma warning restore CA1416
    }

    /// <summary>
    /// Determines whether certificate revocation checks are allowed to go online.
    /// </summary>
    /// <remarks>
    /// Reads <c>AllowOnlineRevocationChecks</c> from the workload registry policy key.
    /// When the policy is absent (default), online checks are allowed. When set to <c>0</c>,
    /// only the locally-cached CRL is used. This affects both <see cref="IsDotNetSigned"/>
    /// and the MSI Authenticode verification in <see cref="Installer.Windows.MsiPackageCache"/>.
    /// </remarks>
    /// <returns><see langword="true"/> if the policy key is absent or set to a non-zero value; <see langword="false"/> if set to 0.</returns>
    public static bool AllowOnlineRevocationChecks()
    {
        using RegistryKey? policyKey = Registry.LocalMachine.OpenSubKey(WorkloadPolicyKey);
        return ((int?)policyKey?.GetValue(OnlineRevocationCheckPolicyKeyName) ?? 1) != 0;
    }

    /// <summary>
    /// Determines whether the administrator has enabled the global policy to enforce signature
    /// verification for all workload operations.
    /// </summary>
    /// <remarks>
    /// Reads <c>VerifySignatures</c> from <c>HKLM\SOFTWARE\Policies\Microsoft\dotnet\Workloads</c>.
    /// When this policy is set (non-zero), the <c>--skip-sign-check</c> command-line flag is rejected.
    /// See <see cref="WorkloadUtilities.ShouldVerifySignatures"/>.
    /// </remarks>
    /// <returns><see langword="true"/> if the policy requires signature verification; <see langword="false"/> otherwise.</returns>
    public static bool IsWorkloadSignVerificationPolicySet()
    {
        using RegistryKey? policyKey = Registry.LocalMachine.OpenSubKey(WorkloadPolicyKey);
        return ((int?)policyKey?.GetValue(VerifySignaturesPolicyKeyName) ?? 0) != 0;
    }
}
