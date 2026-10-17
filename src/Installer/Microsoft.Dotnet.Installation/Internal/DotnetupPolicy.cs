// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Machine-wide dotnetup policy. On Windows the flags are independent <c>REG_DWORD</c> values under
/// <c>HKLM\SOFTWARE\Policies\Microsoft\dotnet\Dotnetup</c>; elsewhere only the unsigned download
/// block exists, as a sentinel file. Admin mode is Windows-only and is only ever read from this
/// policy, never from the per-user dotnetup configuration.
/// </summary>
/// <remarks>
/// The policy reads its <see cref="IDotnetupPolicyStore"/> on every query, so a change made by an
/// elevated child process is observed by the parent. Construct it with a store to supply policy
/// values from somewhere other than the machine.
/// </remarks>
internal sealed class DotnetupPolicy
{
    internal const string WindowsPolicyKey = @"SOFTWARE\Policies\Microsoft\dotnet\Dotnetup";
    internal const string AdminModeValueName = "AdminMode";
    internal const string BlockUnsignedDownloadsValueName = "BlockUnsignedDownloads";

    private readonly IDotnetupPolicyStore _store;

    /// <summary>Creates a policy backed by the machine policy store for the current platform.</summary>
    public DotnetupPolicy()
        : this(CreateMachineStore())
    {
    }

    public DotnetupPolicy(IDotnetupPolicyStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>Returns true when the machine policy enables admin mode. Always false off Windows.</summary>
    public bool IsAdminModeEnabled() =>
        OperatingSystem.IsWindows() && _store.ReadFlag(AdminModeValueName);

    /// <summary>
    /// Returns true when the machine policy blocks downloads that have only a SHA-512 hash and no
    /// detached signature.
    /// </summary>
    public bool IsUnsignedDownloadBlocked() =>
        _store.ReadFlag(BlockUnsignedDownloadsValueName);

    /// <summary>Enables admin mode, which also blocks unsigned downloads.</summary>
    public void EnableAdminMode() =>
        _store.WriteFlags(new Dictionary<string, bool>
        {
            [AdminModeValueName] = true,
            [BlockUnsignedDownloadsValueName] = true,
        });

    /// <summary>
    /// Disables admin mode. The unsigned download policy is left unchanged because IT may manage it
    /// independently.
    /// </summary>
    public void DisableAdminMode() =>
        _store.WriteFlags(new Dictionary<string, bool>
        {
            [AdminModeValueName] = false,
        });

    private static IDotnetupPolicyStore CreateMachineStore() =>
        OperatingSystem.IsWindows() ? new WindowsRegistryPolicyStore() : new UnixPolicyStore();
}
