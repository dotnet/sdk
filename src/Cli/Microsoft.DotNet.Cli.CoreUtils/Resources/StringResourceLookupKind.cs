// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  The result of looking up a name in a string resource table.
/// </summary>
internal enum StringResourceLookupKind
{
    /// <summary>
    ///  The name does not exist in the resource table.
    /// </summary>
    Missing,

    /// <summary>
    ///  The name identifies an intrinsic string.
    /// </summary>
    Found,

    /// <summary>
    ///  The table was retired while the lookup was waiting to access its backing.
    /// </summary>
    Stale
}