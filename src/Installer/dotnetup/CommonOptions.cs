// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Text;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class CommonOptions
{
    public static Option<bool> InteractiveOption = new("--interactive")
    {
        Description = Strings.CommandInteractiveOptionDescription,
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => !IsCIEnvironmentOrRedirected()
    };

    public static Option<bool> NoProgressOption = new("--no-progress")
    {
        Description = "Disables progress display for operations",
        Arity = ArgumentArity.ZeroOrOne
    };

    public static Option<string> InstallPathOption = new("--install-path")
    {
        HelpName = "INSTALL_PATH",
        Description = "The path to install .NET to",
    };

    public static Option<bool?> SetDefaultInstallOption = new("--set-default-install")
    {
        Description = "Set the install path as the default dotnet install. This will update the PATH and DOTNET_ROOT environment variables.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = r => null
    };

    public static Option<string> ManifestPathOption = new("--manifest-path")
    {
        HelpName = "MANIFEST_PATH",
        Description = "Custom path to the manifest file for tracking .NET installations",
    };

    public static Option<bool> RequireMuxerUpdateOption = new("--require-muxer-update")
    {
        Description = "Fail if the muxer (dotnet executable) cannot be updated. By default, a warning is displayed but installation continues.",
        Arity = ArgumentArity.ZeroOrOne
    };

    private static bool IsCIEnvironmentOrRedirected() =>
        new Cli.Telemetry.CIEnvironmentDetectorForTelemetry().IsCIEnvironment() || Console.IsOutputRedirected;
}
