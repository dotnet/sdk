// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources;

/// <summary>
///  Controls string resource table loading behavior.
/// </summary>
[Flags]
public enum StringResourceManagerOptions
{
    /// <summary>
    ///  Reject tables containing null or non-string resources.
    /// </summary>
    None = 0,

    /// <summary>
    ///  Permit non-string resources but do not retain their names or values. A string lookup for an
    ///  ignored resource behaves as though the name is missing. Null resources remain invalid.
    /// </summary>
    IgnoreNonStringResources = 1
}