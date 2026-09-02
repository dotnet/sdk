// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Identifies the work required to make a file-based application build current.
/// </summary>
internal enum BuildLevel
{
    /// <summary>Build outputs are up to date.</summary>
    None,

    /// <summary>Only direct C# compilation is needed.</summary>
    Csc,

    /// <summary>MSBuild is needed.</summary>
    All,
}
