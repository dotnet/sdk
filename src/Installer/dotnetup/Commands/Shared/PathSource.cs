// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Describes how an install path was determined during path resolution.
/// </summary>
internal enum PathSource
{
    /// <summary>The user explicitly specified the install path (e.g., --install-path option).</summary>
    Explicit,

    /// <summary>The install path came from a global.json sdk-path.</summary>
    GlobalJson,

    /// <summary>An existing user-level .NET installation was found and reused.</summary>
    ExistingUserInstall,

    /// <summary>The user was prompted interactively and chose a path.</summary>
    InteractivePrompt,

    /// <summary>No other source applied; the default install path was used.</summary>
    Default,
}
