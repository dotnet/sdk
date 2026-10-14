// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
#if NET
using Microsoft.Win32;
#endif

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Reads and writes the machine-wide dotnetup policy flags.
/// </summary>
internal interface IDotnetupPolicyStore
{
    /// <summary>
    /// Returns true when the named flag is set to a nonzero value. Returns false when the value is
    /// missing, unreadable, or not a DWORD.
    /// </summary>
    bool ReadFlag(string valueName);

    /// <summary>
    /// Writes each flag as a DWORD 1 (true) or 0 (false), creating the policy key if needed.
    /// Throws when the values cannot be written.
    /// </summary>
    void WriteFlags(IReadOnlyDictionary<string, bool> values);
}

/// <summary>
/// Machine-wide dotnetup policy stored as independent <c>REG_DWORD</c> values under
/// <c>HKLM\SOFTWARE\Policies\Microsoft\dotnet\Dotnetup</c>. Admin mode is Windows-only and is only
/// ever read from this policy store, never from the per-user dotnetup configuration.
/// </summary>
internal static class DotnetupPolicy
{
    internal const string WindowsPolicyKey = @"SOFTWARE\Policies\Microsoft\dotnet\Dotnetup";
    internal const string AdminModeValueName = "AdminMode";
    internal const string BlockUnsignedDownloadsValueName = "BlockUnsignedDownloads";

    /// <summary>
    /// The store backing the policy. Defaults to the Windows registry; tests may replace it and must
    /// call <see cref="ResetStoreForTesting"/> afterwards.
    /// </summary>
    internal static IDotnetupPolicyStore Store { get; set; } = CreateDefaultStore();

    internal static void ResetStoreForTesting() => Store = CreateDefaultStore();

    /// <summary>Returns true when the machine policy enables admin mode. Always false off Windows.</summary>
    public static bool IsAdminModeEnabled() =>
        OperatingSystem.IsWindows() && Store.ReadFlag(AdminModeValueName);

    /// <summary>Returns true when the Windows registry policy blocks unsigned downloads.</summary>
    public static bool IsUnsignedDownloadBlockedByRegistry() =>
        Store.ReadFlag(BlockUnsignedDownloadsValueName);

    /// <summary>Enables admin mode, which also blocks unsigned downloads.</summary>
    public static void EnableAdminMode() =>
        Store.WriteFlags(new Dictionary<string, bool>
        {
            [AdminModeValueName] = true,
            [BlockUnsignedDownloadsValueName] = true,
        });

    /// <summary>
    /// Disables admin mode. The unsigned download policy is left unchanged because IT may manage it
    /// independently.
    /// </summary>
    public static void DisableAdminMode() =>
        Store.WriteFlags(new Dictionary<string, bool>
        {
            [AdminModeValueName] = false,
        });

    private static IDotnetupPolicyStore CreateDefaultStore() =>
        OperatingSystem.IsWindows() ? new WindowsRegistryPolicyStore() : new UnsupportedPlatformPolicyStore();

    [SupportedOSPlatform("windows")]
    private sealed class WindowsRegistryPolicyStore : IDotnetupPolicyStore
    {
        public bool ReadFlag(string valueName)
        {
#if NET
            try
            {
                using RegistryKey baseKey = OpenLocalMachine();
                using RegistryKey? key = baseKey.OpenSubKey(WindowsPolicyKey);
                return key?.GetValue(valueName) is int value && value != 0;
            }
            catch
            {
                return false;
            }
#else
            return false;
#endif
        }

        public void WriteFlags(IReadOnlyDictionary<string, bool> values)
        {
            ArgumentNullException.ThrowIfNull(values);
#if NET
            using RegistryKey baseKey = OpenLocalMachine();
            using RegistryKey key = baseKey.CreateSubKey(WindowsPolicyKey, writable: true)
                ?? throw new InvalidOperationException($@"Unable to open HKLM\{WindowsPolicyKey} for writing.");
            foreach (var (valueName, enabled) in values)
            {
                key.SetValue(valueName, enabled ? 1 : 0, RegistryValueKind.DWord);
            }
#else
            throw new PlatformNotSupportedException();
#endif
        }

#if NET
        private static RegistryKey OpenLocalMachine() =>
            RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine,
                Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
#endif
    }

    private sealed class UnsupportedPlatformPolicyStore : IDotnetupPolicyStore
    {
        public bool ReadFlag(string valueName) => false;

        public void WriteFlags(IReadOnlyDictionary<string, bool> values) =>
            throw new PlatformNotSupportedException("dotnetup machine policy is only supported on Windows.");
    }
}
