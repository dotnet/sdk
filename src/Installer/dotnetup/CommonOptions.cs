// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Text;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Output format for commands that support structured output.
/// </summary>
public enum OutputFormat
{
    /// <summary>Human-readable text output (default).</summary>
    Text,
    /// <summary>Machine-readable JSON output.</summary>
    Json
}

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

    /// <summary>
    /// Output format option for commands that support structured output.
    /// Consistent with dotnet CLI's --format option.
    /// </summary>
    public static Option<OutputFormat> FormatOption = new("--format")
    {
        Description = Strings.FormatOptionDescription,
        Arity = ArgumentArity.ExactlyOne,
        DefaultValueFactory = _ => OutputFormat.Text
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

    private static bool IsCIEnvironmentOrRedirected() =>
        new Cli.Telemetry.CIEnvironmentDetectorForTelemetry().IsCIEnvironment() || Console.IsOutputRedirected;
}
