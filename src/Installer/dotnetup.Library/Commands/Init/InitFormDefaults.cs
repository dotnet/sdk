// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// The defaults and supporting context used to initialize the init form. This data is resolved
/// without side effects; concrete install requests are resolved separately after the user accepts
/// the form.
/// </summary>
/// <param name="InstallRoot">The install root the environment is configured against.</param>
/// <param name="AccessMode">The default access mode.</param>
/// <param name="MigrateSystemInstalls">Whether migration is selected by default.</param>
/// <param name="Migrations">The system installs eligible for migration before pending installs are excluded.</param>
/// <param name="ChannelDisplay">Display information for the default SDK channel.</param>
/// <param name="DefaultInstallSpecs">The component and channel selections represented by the default channel choice.</param>
/// <param name="ShellProvider">The resolved shell provider used to select and describe the default access mode.</param>
/// <param name="InstallRootGlobalJsonPath">The global.json that supplied the install root, if any.</param>
internal sealed record InitFormDefaults(
    DotnetInstallRoot InstallRoot,
    DotnetAccessMode AccessMode,
    bool MigrateSystemInstalls,
    List<MigrationWorkflow.MigrationSelection> Migrations,
    DefaultChannelDisplay ChannelDisplay,
    IReadOnlyList<MinimalInstallSpec> DefaultInstallSpecs,
    IEnvShellProvider? ShellProvider,
    string? InstallRootGlobalJsonPath);
