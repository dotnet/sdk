// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using System.CommandLine.Completions;

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

    public static readonly Option<Verbosity> VerbosityOption = new("--verbosity", "-v")
    {
        Description = "Set the output verbosity level (normal, detailed)",
        Arity = ArgumentArity.ExactlyOne,
        DefaultValueFactory = _ => Verbosity.Normal
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

    public static readonly Option<IEnvShellProvider?> ShellOption = new("--shell", "-s")
    {
        Description = $"The shell to use for profile-based environment configuration (supported: {string.Join(", ", ShellDetection.s_supportedShells.Select(s => s.ArgumentName))}). If not specified, the current shell will be detected.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => ShellDetection.GetCurrentShellProvider(),
        CustomParser = (optionResult) =>
        {
            return optionResult.Tokens switch
            {
                [] => ShellDetection.GetCurrentShellProvider(),
                [var shellToken] => ShellDetection.GetShellProvider(shellToken.Value),
                _ => throw new InvalidOperationException("Unexpected number of tokens")
            };
        },
        Validators = { ValidateShellOption() },
        CompletionSources = { CreateShellCompletions() }
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
    /// Creates a channel argument for SDK commands that accepts multiple values.
    /// Allows commands like: dotnetup sdk install 9.0 10.0
    /// </summary>
    /// <param name="actionVerb">Verb for the description (e.g., "install").</param>
    public static Argument<string[]> CreateSdkChannelArguments(string actionVerb)
    {
        return new Argument<string[]>("channel")
        {
            HelpName = "CHANNEL",
            Description = $"One or more channels or versions of the .NET SDK to {actionVerb} (e.g., latest, 10, 9.0.3xx, or 9.0.304). "
                + $"Multiple channels can be provided to {actionVerb} concurrently.",
            Arity = ArgumentArity.ZeroOrMore,
        };
    }

    /// <summary>
    /// Creates a component-spec argument for runtime commands (single value).
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
    /// Creates a component-spec argument for runtime commands that accepts multiple values.
    /// Allows commands like: dotnetup runtime install aspnet@9.0 runtime@10.0.2
    /// </summary>
    /// <param name="actionVerb">Verb for the description (e.g., "install").</param>
    public static Argument<string[]> CreateRuntimeComponentSpecsArgument(string actionVerb)
    {
        return new Argument<string[]>("component-spec")
        {
            HelpName = "COMPONENT_SPEC",
            Description = $"One or more version/channel (e.g., 10.0) or component@version (e.g., aspnetcore@10.0) to {actionVerb}. "
                + "When only a version is provided, the core .NET runtime is targeted. "
                + $"Multiple specs can be provided to {actionVerb} concurrently. "
                + "Valid component types: " + string.Join(", ", RuntimeInstallCommand.GetValidRuntimeTypes()),
            Arity = ArgumentArity.ZeroOrMore,
        };
    }

    private static bool IsCIEnvironmentOrRedirected() =>
        new Cli.Telemetry.CIEnvironmentDetectorForTelemetry().IsCIEnvironment() || Console.IsOutputRedirected;

    private static Action<System.CommandLine.Parsing.OptionResult> ValidateShellOption()
    {
        return (System.CommandLine.Parsing.OptionResult optionResult) =>
        {
            if (optionResult.Tokens.Count == 0)
            {
                return;
            }

            var shellToken = optionResult.Tokens[0];
            if (!ShellDetection.IsSupported(shellToken.Value))
            {
                optionResult.AddError($"Unsupported shell '{shellToken.Value}'. Supported shells: {string.Join(", ", ShellDetection.s_supportedShells.Select(s => s.ArgumentName))}");
            }
        };
    }

    private static Func<CompletionContext, IEnumerable<CompletionItem>> CreateShellCompletions()
    {
        return _ => ShellDetection.s_supportedShells
            .Select(s => new CompletionItem(s.ArgumentName, documentation: s.HelpDescription));
    }

    internal static Action<System.CommandLine.Parsing.CommandResult> RejectShellOptionOnInstallCommand()
    {
        return commandResult =>
        {
            if (commandResult.Tokens.Any(token => token.Value is "--shell" or "-s"))
            {
                commandResult.AddError(
                    "The --shell option isn't supported on install commands. If you need to override shell detection, run 'dotnetup init --shell <name>' before installing.");
            }
        };
    }
}
