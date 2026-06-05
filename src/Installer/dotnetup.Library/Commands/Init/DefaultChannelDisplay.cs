// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// The information needed to render the "SDK Channel" line of the walkthrough summary.
/// Derived from the already-resolved default install request so the displayed channel
/// matches exactly what would be installed when the user proceeds with defaults.
/// </summary>
/// <param name="ChannelLabel">The resolved channel label (e.g. "10.0", "10.0.1xx", "latest"), or null when there is no SDK to install.</param>
/// <param name="GlobalJsonPath">The path of the global.json that implied the channel, or null when the channel came from the default.</param>
internal sealed record DefaultChannelDisplay(string? ChannelLabel, string? GlobalJsonPath);
