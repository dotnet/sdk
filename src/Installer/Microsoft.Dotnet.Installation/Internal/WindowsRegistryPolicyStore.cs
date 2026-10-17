// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Stores dotnetup policy flags as <c>REG_DWORD</c> values under
/// <c>HKLM\SOFTWARE\Policies\Microsoft\dotnet\Dotnetup</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsRegistryPolicyStore : IDotnetupPolicyStore
{
    public bool ReadFlag(string valueName)
    {
        try
        {
            using RegistryKey baseKey = OpenLocalMachine();
            using RegistryKey? key = baseKey.OpenSubKey(DotnetupPolicy.WindowsPolicyKey);
            return key?.GetValue(valueName) is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }

    public void WriteFlags(IReadOnlyDictionary<string, bool> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        using RegistryKey baseKey = OpenLocalMachine();
        using RegistryKey key = baseKey.CreateSubKey(DotnetupPolicy.WindowsPolicyKey, writable: true)
            ?? throw new InvalidOperationException($@"Unable to open HKLM\{DotnetupPolicy.WindowsPolicyKey} for writing.");
        foreach (var (valueName, enabled) in values)
        {
            key.SetValue(valueName, enabled ? 1 : 0, RegistryValueKind.DWord);
        }
    }

    private static RegistryKey OpenLocalMachine() =>
        RegistryKey.OpenBaseKey(
            RegistryHive.LocalMachine,
            Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
}
