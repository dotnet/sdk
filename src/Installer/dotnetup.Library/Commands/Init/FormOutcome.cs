// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// The user's raw init choices, captured from the interactive form (or the recommended defaults in
/// non-interactive sessions) before any concrete install requests are resolved. Keeping this
/// separate from <see cref="WalkthroughSelection"/> lets dry-run preview the decision without
/// resolving versions or touching the network.
/// </summary>
/// <param name="Channel">
/// The selected channel token or typed custom value. In non-interactive sessions this may be
/// <c>null</c> to indicate that the plan's default install requests should be used unchanged.
/// </param>
/// <param name="AccessMode">The chosen dotnet access mode.</param>
/// <param name="Migrate">True when the user chose to migrate existing system installs.</param>
internal sealed record FormOutcome(
    string? Channel,
    DotnetAccessMode AccessMode,
    bool Migrate);
