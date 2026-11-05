// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Policy store used off Windows. The only machine policy there is the unsigned download
/// block, which is enforced when the sentinel file <see cref="UnsignedSourcePolicy.UnixPolicyFile"/>
/// exists (its content is ignored).
/// </summary>
internal sealed class UnixPolicyStore : IDotnetupPolicyStore
{
    private readonly string _blockUnsignedDownloadsFile;

    public UnixPolicyStore()
        : this(UnsignedSourcePolicy.UnixPolicyFile)
    {
    }

    internal UnixPolicyStore(string blockUnsignedDownloadsFile)
    {
        _blockUnsignedDownloadsFile = blockUnsignedDownloadsFile;
    }

    public bool ReadFlag(string valueName)
    {
        if (!string.Equals(valueName, DotnetupPolicy.BlockUnsignedDownloadsValueName, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            return File.Exists(_blockUnsignedDownloadsFile);
        }
        catch
        {
            // Reading /etc shouldn't fault, but be defensive — a failed probe must not
            // promote a permissive default into a hard block.
            return false;
        }
    }
}
