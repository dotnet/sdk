// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Reads the machine-wide dotnetup policy flags. <see cref="DotnetupPolicy"/> owns the meaning of
/// each flag; a store only reads them.
/// </summary>
internal interface IDotnetupPolicyStore
{
    /// <summary>
    /// Returns true when the named flag is set to a nonzero value. Returns false when the value is
    /// missing, unreadable, or not a DWORD.
    /// </summary>
    bool ReadFlag(string valueName);
}
