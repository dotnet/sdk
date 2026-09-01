// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// The resolved work to execute after the user accepts the init form or the equivalent
/// non-interactive defaults.
/// </summary>
/// <param name="InstallRequests">The install requests to execute.</param>
/// <param name="AccessMode">The access mode to apply and persist.</param>
/// <param name="Migrations">The system installs to migrate alongside the install.</param>
internal sealed record InitExecutionPlan(
    List<ResolvedInstallRequest> InstallRequests,
    DotnetAccessMode AccessMode,
    List<MigrationWorkflow.MigrationSelection> Migrations);
