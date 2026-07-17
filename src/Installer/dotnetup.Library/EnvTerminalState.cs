// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Describes how the <em>current terminal's</em> live PATH compares to the configured env state,
/// split into whether bringing it in line would require adding managed entries, removing stale
/// ones, or both. This distinction matters because the paste-able activation command can only
/// <em>add</em> managed directories to the running session — it cannot remove a stale entry — so a
/// state that needs removals can only be fully resolved by opening a new terminal.
/// </summary>
internal sealed record EnvTerminalState(bool NeedsAdditions, bool NeedsRemovals)
{
    /// <summary>The current terminal already matches the configured state; nothing to do.</summary>
    public bool IsActive => !NeedsAdditions && !NeedsRemovals;
}
