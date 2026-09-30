// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources;

/// <summary>
///  Controls how direct satellite assembly probe failures affect culture fallback.
/// </summary>
public enum SatelliteStringResourceProbeMode
{
    /// <summary>
    ///  Continue fallback only when the candidate file is absent. Other failures throw.
    /// </summary>
    Strict,

    /// <summary>
    ///  Treat absent, unreadable, malformed, unsupported, incorrectly bundled, or
    ///  identity-mismatched localized candidates as missing and continue fallback.
    /// </summary>
    FallbackOnFailure
}
