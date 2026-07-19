// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// The user's raw init choices, captured from the interactive form (or the recommended defaults in
/// non-interactive sessions) before any concrete install requests are resolved. Keeping this
/// separate from <see cref="WalkthroughSelection"/> lets dry-run preview the decision without
/// resolving versions or touching the network.
/// </summary>
/// <param name="SkipInstall">True when the user chose not to install an SDK now ("none").</param>
/// <param name="Channel">
/// The chosen channel token (or typed custom value) when the channel was changed from the default;
/// <c>null</c> when the default channel is unchanged or when <paramref name="SkipInstall"/> is true.
/// </param>
/// <param name="ChannelChanged">True when the user changed the channel away from the default.</param>
/// <param name="AccessMode">The chosen dotnet access mode.</param>
/// <param name="Migrate">True when the user chose to migrate existing system installs.</param>
internal sealed record FormOutcome(
    bool SkipInstall,
    string? Channel,
    bool ChannelChanged,
    DotnetAccessMode AccessMode,
    bool Migrate);
