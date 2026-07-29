// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Describes whether the conservative source probe found possible file directives.
/// </summary>
internal enum FileBasedAppDirectiveProbeResult
{
    /// <summary>No file-directive byte sequence was found.</summary>
    None,

    /// <summary>A caller with exact directive information reports that directives are present.</summary>
    Present,

    /// <summary>The source may contain directives or could not be inspected safely.</summary>
    Unknown,
}
