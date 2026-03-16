// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;

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
    public static readonly Option<bool> InteractiveOption = new("--interactive")
    {
        Description = Strings.CommandInteractiveOptionDescription,
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => !IsCIEnvironmentOrRedirected()
    };

    public static readonly Option<bool> NoProgressOption = new("--no-progress")
    {
        Description = "Disables progress display for operations",
        Arity = ArgumentArity.ZeroOrOne
    };

    /// <summary>
    /// Output format option for commands that support structured output.
    /// Consistent with dotnet CLI's --format option.
    /// </summary>
    public static readonly Option<OutputFormat> FormatOption = new("--format")
    {
        Description = Strings.FormatOptionDescription,
        Arity = ArgumentArity.ExactlyOne,
        DefaultValueFactory = _ => OutputFormat.Text
    };

    public static readonly Option<string> InstallPathOption = new("--install-path")
    {
        HelpName = "INSTALL_PATH",
        Description = "The path to install .NET to",
    };

    public static readonly Option<bool?> SetDefaultInstallOption = new("--set-default-install")
    {
        Description = "Set the install path as the default dotnet install. This will update the PATH and DOTNET_ROOT environment variables.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = r => null
    };

    public static readonly Option<string> ManifestPathOption = new("--manifest-path")
    {
        HelpName = "MANIFEST_PATH",
        Description = "Custom path to the manifest file for tracking .NET installations",
    };

    public static readonly Option<InstallSource> SourceOption = new("--source")
    {
        Description = "Filter by install source (explicit, globaljson, all).",
        DefaultValueFactory = _ => InstallSource.Explicit
    };

    public static readonly Option<bool> RequireMuxerUpdateOption = new("--require-muxer-update")
    {
        Description = "Fail if the dotnet executable cannot be updated. By default, a warning is displayed but installation continues.",
        Arity = ArgumentArity.ZeroOrOne
    };

    public static readonly Option<bool> UntrackedOption = new("--untracked")
    {
        Description = "Install without recording in the dotnetup manifest. The installation will not be tracked, updated, or listed by dotnetup.",
        Arity = ArgumentArity.ZeroOrOne
    };

    /// <summary>
    /// Creates a channel argument for SDK commands.
    /// Each command needs its own Argument instance (System.CommandLine requirement),
    /// but the shape is shared.
    /// </summary>
    /// <param name="required">
    /// If true, the argument is required (arity = ExactlyOne).
    /// If false, the argument is optional (arity = ZeroOrOne).
    /// </param>
    /// <param name="actionVerb">Verb for the description (e.g., "install", "remove").</param>
    public static Argument<string?> CreateSdkChannelArgument(bool required, string actionVerb)
    {
        return new Argument<string?>("channel")
        {
            HelpName = "CHANNEL",
            Description = $"The channel or version of the .NET SDK to {actionVerb} (e.g., latest, 10, 9.0.3xx, or 9.0.304).",
            Arity = required ? ArgumentArity.ExactlyOne : ArgumentArity.ZeroOrOne,
        };
    }

    /// <summary>
    /// Creates a component-spec argument for runtime commands.
    /// Each command needs its own Argument instance (System.CommandLine requirement),
    /// but the shape and valid types are shared.
    /// </summary>
    /// <param name="required">
    /// If true, the argument is required (arity = ExactlyOne).
    /// If false, the argument is optional (arity = ZeroOrOne).
    /// </param>
    /// <param name="actionVerb">Verb for the description (e.g., "install", "uninstall").</param>
    public static Argument<string?> CreateRuntimeComponentSpecArgument(bool required, string actionVerb)
    {
        return new Argument<string?>("component-spec")
        {
            HelpName = "COMPONENT_SPEC",
            Description = $"The version/channel (e.g., 10.0) or component@version (e.g., aspnetcore@10.0) to {actionVerb}. "
                + "When only a version is provided, the core .NET runtime is targeted. "
                + "Valid component types: " + string.Join(", ", RuntimeInstallCommand.GetValidRuntimeTypes()),
            Arity = required ? ArgumentArity.ExactlyOne : ArgumentArity.ZeroOrOne,
        };
    }

    /// <summary>
    /// Creates a positional argument that accepts one or more runtime component specifications.
    /// Used by runtime install to support installing multiple runtimes in a single invocation.
    /// </summary>
    /// <param name="actionVerb">Verb for the description (e.g., "install").</param>
    public static Argument<string[]> CreateMultipleRuntimeComponentSpecArgument(string actionVerb)
    {
        return new Argument<string[]>("component-spec")
        {
            HelpName = "COMPONENT_SPEC",
            Description = $"One or more version/channel (e.g., 10.0) or component@version (e.g., aspnetcore@10.0) to {actionVerb}. "
                + "When only a version is provided, the core .NET runtime is targeted. "
                + "Multiple specs can be provided to {actionVerb} several runtimes at once. "
                + "Valid component types: " + string.Join(", ", RuntimeInstallCommand.GetValidRuntimeTypes()),
            Arity = ArgumentArity.ZeroOrMore,
        };
    }

    private static bool IsCIEnvironmentOrRedirected() =>
        new Cli.Telemetry.CIEnvironmentDetectorForTelemetry().IsCIEnvironment() || Console.IsOutputRedirected;
}
