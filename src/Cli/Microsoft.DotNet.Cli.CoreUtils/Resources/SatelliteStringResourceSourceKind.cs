// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  The selected source for localized string tables.
/// </summary>
internal enum SatelliteStringResourceSourceKind
{
    /// <summary>
    ///  Bind satellite assemblies through the runtime.
    /// </summary>
    RuntimeSatellites,

    /// <summary>
    ///  Read loose binary resource files.
    /// </summary>
    ResourcesDirectory,

    /// <summary>
    ///  Parse satellite assemblies directly as data.
    /// </summary>
    SatelliteDirectory
}