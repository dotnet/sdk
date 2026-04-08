// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Installer.Windows.Security;
using Microsoft.Win32;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal static class SignCheck
{
    private const string OnlineRevocationCheckPolicyKeyName = "AllowOnlineRevocationChecks";
    private const string VerifySignaturesPolicyKeyName = "VerifySignatures";
    private const string WorkloadPolicyKey = @"SOFTWARE\Policies\Microsoft\dotnet\Workloads";

    private static readonly string? s_dotnet = Environment.ProcessPath;

    /// <summary>
    /// Determines whether dotnet.dll is signed.
    /// </summary>
    /// <returns><see langword="true"/> if dotnet is signed; otherwise, <see langword="false"/>.</returns>
    public static bool IsDotNetSigned()
    {
        Debug.Assert(s_dotnet is not null, "Environment.ProcessPath should not be null when running in the dotnet host.");

        if (s_dotnet is null)
        {
            return false;
        }

        // API is only available on XP and Server 2003 or later versions. .NET requires Win7 minimum.
#pragma warning disable CA1416
        // We don't care about trust in this case, only whether or not the file has a signature as that determines
        // whether we'll trigger sign verification for workload operations.
        return Signature.IsAuthenticodeSigned(s_dotnet, AllowOnlineRevocationChecks()) == 0;
#pragma warning restore CA1416
    }

    /// <summary>
    /// Determines whether revocation checks can go online based on the global policy setting in the registry.
    /// </summary>
    /// <returns><see langword="true"/> if the policy key is absent or set to a non-zero value; <see langword="false"/> if the policy key is set to 0.</returns>
    public static bool AllowOnlineRevocationChecks()
    {
        using RegistryKey? policyKey = Registry.LocalMachine.OpenSubKey(WorkloadPolicyKey);
        return ((int?)policyKey?.GetValue(OnlineRevocationCheckPolicyKeyName) ?? 1) != 0;
    }

    /// <summary>
    /// Determines whether the global policy to enforce signature checks for workloads is set.
    /// </summary>
    /// <returns><see langword="true"/> if the policy is set; <see langword="false"/> otherwise.</returns>
    public static bool IsWorkloadSignVerificationPolicySet()
    {
        using RegistryKey? policyKey = Registry.LocalMachine.OpenSubKey(WorkloadPolicyKey);
        return ((int?)policyKey?.GetValue(VerifySignaturesPolicyKeyName) ?? 0) != 0;
    }
}
