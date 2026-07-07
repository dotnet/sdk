// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// The setup the walkthrough resolved to execute after the user made their choice
/// (either accepting the defaults or customizing). A null selection signals that the
/// user chose to exit without making any changes.
/// </summary>
/// <param name="Requests">The install requests to execute.</param>
/// <param name="PathPreference">The path preference (mode) to apply and persist.</param>
/// <param name="Migrations">The system installs to migrate alongside the install.</param>
internal sealed record WalkthroughSelection(
    List<ResolvedInstallRequest> Requests,
    PathPreference PathPreference,
    List<MigrationWorkflow.MigrationSelection> Migrations);
