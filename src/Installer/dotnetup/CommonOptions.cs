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

    private static bool IsCIEnvironmentOrRedirected() =>
        new Cli.Telemetry.CIEnvironmentDetectorForTelemetry().IsCIEnvironment() || Console.IsOutputRedirected;
}
