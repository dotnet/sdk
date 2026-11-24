// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Whether the managed dotnetup block is present in the shell profile, as observed by
/// <see cref="EnvironmentStateInspector"/>. <see cref="Unknown"/> means the shell could not be
/// determined, so neither presence nor absence is asserted (callers must not act on it).
/// </summary>
internal enum ProfileBlockState
{
    /// <summary>The shell could not be determined; profile state was not inspected.</summary>
    Unknown,

    /// <summary>The managed dotnetup block is present in the shell profile.</summary>
    Present,

    /// <summary>The shell is known and the managed dotnetup block is absent from its profile.</summary>
    Absent,
}
