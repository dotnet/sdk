// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli;

internal static class CommonOptions
{
    public static Option<bool> CreateYesOption() => new("--yes", "-y")
    {
        Description = CommandDefinitionStrings.YesOptionDescription,
        Arity = ArgumentArity.Zero,
        IsDynamic = true
    };

    public static Option<ReadOnlyDictionary<string, string>?> CreatePropertyOption() =>
        // these are all of the forms that the property switch can be understood by in MSBuild
        new Option<ReadOnlyDictionary<string, string>?>("--property", "-property", "/property", "/p", "-p", "--p")
        {
            Hidden = true,
            Arity = ArgumentArity.ZeroOrMore,
            CustomParser = ParseMSBuildTokensIntoDictionary
        }.ForwardAsMSBuildProperty()
         .AllowSingleArgPerToken();

    /// <summary>
    /// Sets MSBuild Global Property values that are only used during Restore (implicit or explicit)
    /// </summary>
    /// <remarks>
    /// </remarks>
    public static Option<ReadOnlyDictionary<string, string>?> CreateRestorePropertyOption() =>
        // these are all of the forms that the property switch can be understood by in MSBuild
        new Option<ReadOnlyDictionary<string, string>?>("--restoreProperty", "-restoreProperty", "/restoreProperty", "-rp", "--rp", "/rp")
        {
            Hidden = true,
            Arity = ArgumentArity.ZeroOrMore,
            CustomParser = ParseMSBuildTokensIntoDictionary
        }
        .ForwardAsMSBuildProperty()
        .AllowSingleArgPerToken();

    private static ReadOnlyDictionary<string, string>? ParseMSBuildTokensIntoDictionary(ArgumentResult result)
    {
        if (result.Tokens.Count == 0)
        {
            return null;
        }
        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in result.Tokens)
        {
            foreach (var kvp in MSBuildPropertyParser.ParseProperties(token.Value))
            {
                // msbuild properties explicitly have the semantic of being 'overwrite' so we do not check for duplicates
                // and just overwrite the value if it already exists.
                dictionary[kvp.key] = kvp.value;
            }
        }
        return new(dictionary);
    }

    public static Option<string[]?> CreateMSBuildTargetOption(string? defaultTargetName = null, (string key, string value)[]? additionalProperties = null) =>
        new Option<string[]?>("--target", "/target", "-target", "-t", "--t", "/t")
        {
            Description = "Build these targets in this project. Use a semicolon or a comma to separate multiple targets, or specify each target separately.",
            HelpName = "TARGET",
            DefaultValueFactory = _ => defaultTargetName is not null ? [defaultTargetName] : null,
            CustomParser = r => SplitMSBuildValues(defaultTargetName, r),
            Hidden = true,
            Arity = ArgumentArity.ZeroOrMore
        }
        .ForwardAsMany(targets => ForwardTargetsAndAdditionalProperties(targets, additionalProperties))
        .AllowSingleArgPerToken();

    public static Option<string[]> CreateRequiredMSBuildTargetOption(string defaultTargetName, (string key, string value)[]? additionalProperties = null) =>
        new Option<string[]>("--target", "/target", "-target", "-t", "--t", "/t")
        {
            Description = "Build these targets in this project. Use a semicolon or a comma to separate multiple targets, or specify each target separately.",
            HelpName = "TARGET",
            DefaultValueFactory = _ => [defaultTargetName],
            CustomParser = r => SplitMSBuildValues(defaultTargetName, r),
            Hidden = true,
            Arity = ArgumentArity.ZeroOrMore
        }
        // we know there will be at least one target, so we return an enumerable with at least one item
        .ForwardAsMany(targets => ForwardTargetsAndAdditionalProperties(targets, additionalProperties))
        .AllowSingleArgPerToken();

    public static IEnumerable<string> ForwardTargetsAndAdditionalProperties(string[]? targets, (string key, string value)[]? additionalProperties)
    {
        var argsToReturn = new List<string>(targets is null ? 0 : 1 + (additionalProperties?.Length ?? 0));
        if (targets is not null)
        {
            argsToReturn.Add($"--target:{string.Join(";", targets)}");
        }
        if (additionalProperties is not null)
        {
            argsToReturn.AddRange(additionalProperties.Select(p => $"--property:{p.key}={p.value}"));
        }
        return argsToReturn;
    }

    public static Option<string[]?> CreateGetPropertyOption() => MSBuildMultiOption("getProperty");

    public static Option<string[]?> CreateGetItemOption() => MSBuildMultiOption("getItem");

    public static Option<string[]?> CreateGetTargetResultOption() => MSBuildMultiOption("getTargetResult");

    public static Option<string[]?> CreateGetResultOutputFileOption() => MSBuildMultiOption("getResultOutputFile");

    private static Option<string[]?> MSBuildMultiOption(string name)
        => new Option<string[]?>($"--{name}", $"-{name}", $"/{name}")
        {
            Hidden = true,
            Arity = ArgumentArity.OneOrMore,
            CustomParser = static r => SplitMSBuildValues(null, r),
        }
        .ForwardAsMany(xs => (xs ?? []).Select(x => $"--{name}:{x}"))
        .AllowSingleArgPerToken();

    public static string[] SplitMSBuildValues(string? defaultValue, ArgumentResult argumentResult)
    {
        if (argumentResult.Tokens.Count == 0)
        {
            return defaultValue is not null ? [defaultValue] : [];
        }
        var userValues =
            argumentResult.Tokens.Select(t => t.Value)
            .SelectMany(t => t.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(t => !string.IsNullOrEmpty(t));
        var allValues = defaultValue is null ? userValues : [defaultValue, .. userValues];
        return allValues.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static Option<VerbosityOptions> CreateVerbosityOption(VerbosityOptions defaultVerbosity) =>
        new Option<VerbosityOptions>("--verbosity", "-v")
        {
            Description = CommandDefinitionStrings.VerbosityOptionDescription,
            HelpName = CommandDefinitionStrings.LevelArgumentName,
            DefaultValueFactory = _ => defaultVerbosity
        }
        .ForwardAsSingle(o => $"--verbosity:{o}")
        .AggregateRepeatedTokens();

    public static Option<VerbosityOptions?> CreateVerbosityOption() =>
        new Option<VerbosityOptions?>("--verbosity", "-v", "--v", "-verbosity", "/v", "/verbosity")
        {
            Description = CommandDefinitionStrings.VerbosityOptionDescription,
            HelpName = CommandDefinitionStrings.LevelArgumentName
        }
        .ForwardAsSingle(o => $"--verbosity:{o}")
        .AggregateRepeatedTokens();

    public static Option<VerbosityOptions> CreateHiddenVerbosityOption() =>
        new Option<VerbosityOptions>("--verbosity", "-v", "--v", "-verbosity", "/v", "/verbosity")
        {
            Description = CommandDefinitionStrings.VerbosityOptionDescription,
            HelpName = CommandDefinitionStrings.LevelArgumentName,
            Hidden = true
        }
        .ForwardAsSingle(o => $"--verbosity:{o}")
        .AggregateRepeatedTokens();

    public const string FrameworkOptionName = "--framework";

    public static Option<string> CreateFrameworkOption(string description) =>
        new Option<string>(FrameworkOptionName, "-f")
        {
            Description = description,
            HelpName = CommandDefinitionStrings.FrameworkArgumentName,
            IsDynamic = true
        }
        .ForwardAsSingle(o => $"--property:TargetFramework={o}");

    public static Option<string> CreateArtifactsPathOption() =>
        new Option<string>(
            //  --artifacts-path is pretty verbose, should we use --artifacts instead (or possibly support both)?
            "--artifacts-path")
        {
            Description = CommandDefinitionStrings.ArtifactsPathOptionDescription,
            HelpName = CommandDefinitionStrings.ArtifactsPathArgumentName
        }.ForwardAsSingle(o => $"--property:ArtifactsPath={CommandDirectoryContext.GetFullPath(o)}");

    public static Option<bool> CreateUseCurrentRuntimeOption(string description) =>
        new Option<bool>("--use-current-runtime", "--ucr")
        {
            Description = description,
            Arity = ArgumentArity.Zero
        }.ForwardAs("--property:UseCurrentRuntimeIdentifier=True");

    public const string ConfigurationOptionName = "--configuration";

    public static Option<string?> CreateConfigurationOption(string description) =>
        new Option<string?>(ConfigurationOptionName, "-c")
        {
            Description = description,
            HelpName = CommandDefinitionStrings.ConfigurationArgumentName,
            IsDynamic = true
        }.ForwardAsSingle(o => $"--property:Configuration={o}");

    public static Option<string> CreateVersionSuffixOption() =>
        new Option<string>("--version-suffix")
        {
            Description = CommandDefinitionStrings.CmdVersionSuffixDescription,
            HelpName = CommandDefinitionStrings.VersionSuffixArgumentName
        }.ForwardAsSingle(o => $"--property:VersionSuffix={o}");

    public static Lazy<string> NormalizedCurrentDirectory = new(() => PathUtilities.EnsureTrailingSlash(Directory.GetCurrentDirectory()));

    public static Argument<string> DefaultToCurrentDirectory(this Argument<string> arg)
    {
        // we set this lazily so that we don't pay the overhead of determining the
        // CWD multiple times, one for each Argument that uses this.
        arg.DefaultValueFactory = _ => NormalizedCurrentDirectory.Value;
        return arg;
    }

    public static Option<bool> CreateNoRestoreOption() => new Option<bool>("--no-restore")
    {
        Description = CommandDefinitionStrings.NoRestoreDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-restore:false");

    public static Option<bool> RestoreOption = new Option<bool>("--restore", "-restore")
    {
        Description = "Restore the project before building it. This is the default behavior.",
        Arity = ArgumentArity.Zero,
        Hidden = true
    }.ForwardAs("-restore");

    private static bool IsCIEnvironmentOrRedirected() =>
        new Telemetry.CIEnvironmentDetectorForTelemetry().IsCIEnvironment() || Console.IsOutputRedirected;

    public const string InteractiveOptionName = "--interactive";

    /// <summary>
    /// A 'template' for interactive usage across the whole dotnet CLI. Use this as a base and then specialize it for your use cases.
    /// Despite being a 'forwarded option' there is no default forwarding configured, so if you want forwarding you can add it on a per-command basis.
    /// </summary>
    /// <param name="acceptArgument">Whether the option accepts an boolean argument. If false, the option will be a flag.</param>
    /// <remarks>
    /// If not set by a user, this will default to true if the user is not in a CI environment as detected by <see cref="Telemetry.CIEnvironmentDetectorForTelemetry.IsCIEnvironment"/>.
    /// If this is set to function as a flag, then there is no simple user-provided way to circumvent the behavior.
    /// </remarks>
    public static Option<bool> CreateInteractiveOption(bool acceptArgument = false, bool hidden = false) =>
        new(InteractiveOptionName)
        {
            Description = CommandDefinitionStrings.CommandInteractiveOptionDescription,
            Arity = acceptArgument ? ArgumentArity.ZeroOrOne : ArgumentArity.Zero,
            // this default is called when no tokens/options are passed on the CLI args
            DefaultValueFactory = (ar) => !IsCIEnvironmentOrRedirected(),
            Hidden = hidden,
        };

    public static Option<bool> CreateInteractiveMsBuildForwardOption()
        => CreateInteractiveOption(acceptArgument: true)
           .ForwardAsSingle(b => $"--property:NuGetInteractive={(b ? "true" : "false")}");

    public static Option<bool> CreateDisableBuildServersOption() =>
        new Option<bool>("--disable-build-servers")
        {
            Description = CommandDefinitionStrings.DisableBuildServersOptionDescription,
            Arity = ArgumentArity.Zero
        }
        .ForwardIfEnabled(["--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false"]);

    public static Option<bool> DebugOption = new("--debug")
    {
        Arity = ArgumentArity.Zero,
    };

    public static Option<bool> CreateSelfContainedOption() =>
        new Option<bool>("--self-contained", "--sc")
        {
            Description = CommandDefinitionStrings.SelfContainedOptionDescription
        }
        .ForwardIfEnabled([$"--property:SelfContained=true", "--property:_CommandLineDefinedSelfContained=true"]);

    public static Option<bool> CreateNoSelfContainedOption() =>
        new Option<bool>("--no-self-contained")
        {
            Description = CommandDefinitionStrings.FrameworkDependentOptionDescription,
            Arity = ArgumentArity.Zero
        }
        .ForwardIfEnabled([$"--property:SelfContained=false", "--property:_CommandLineDefinedSelfContained=true"]);

    public static Option<IReadOnlyDictionary<string, string>> CreateEnvOption(string? description = null) => new("--environment", "-e")
    {
        Description = description ?? CommandDefinitionStrings.CmdEnvironmentVariableDescription,
        HelpName = CommandDefinitionStrings.CmdEnvironmentVariableExpression,
        CustomParser = ParseEnvironmentVariables,
        // Can't allow multiple arguments because the separator needs to be parsed as part of the environment variable value.
        AllowMultipleArgumentsPerToken = false
    };

    private static IReadOnlyDictionary<string, string> ParseEnvironmentVariables(ArgumentResult argumentResult)
    {
        var result = new Dictionary<string, string>(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        List<Token>? invalid = null;

        foreach (var token in argumentResult.Tokens)
        {
            var separator = token.Value.IndexOf('=');
            var (name, value) = (separator >= 0)
                ? (token.Value[0..separator], token.Value[(separator + 1)..])
                : (token.Value, "");

            name = name.Trim();

            if (name != "")
            {
                result[name] = value;
            }
            else
            {
                invalid ??= [];
                invalid.Add(token);
            }
        }

        if (invalid != null)
        {
            argumentResult.AddError(string.Format(
                CommandDefinitionStrings.IncorrectlyFormattedEnvironmentVariables,
                string.Join(", ", invalid.Select(x => $"'{x.Value}'"))));
        }

        return result;
    }

    /// <summary>
    /// Creates an implementation of the <c>--no-logo</c> option.
    /// This option suppresses the display of the startup banner or logos for commands or products it is applied to.
    /// The behavior of this option is influenced by the <c>DOTNET_NOLOGO</c> environment variable if it is set:
    /// <list type="bullet">
    /// <item>If the <c>--no-logo</c> option is not specified on the command line, the environment variable will be checked to determine
    /// whether to suppress logos. Any truthy value - <c>true</c>, <c>1</c>, <c>yes</c>, <c>on</c> - will suppress logos, while any falsy value - <c>false</c>, <c>0</c>, <c>no</c>, <c>off</c> - will show logos.</item>
    /// <item>If the option is specified on the command line, it takes precedence over the environment variable.</item>
    /// </list>
    /// Finally, if neither the option nor the environment variable is set, the option will default to the provided <paramref name="defaultValue"/>.
    /// </summary>
    public static Option<bool> CreateNoLogoOption(bool defaultValue = true, string forwardAs = "--nologo", string? description = null)
    {
        return new Option<bool>("--no-logo", "--nologo", "-nologo", "/nologo")
        {
            Description = description ?? CommandDefinitionStrings.NoLogoOptionDescription,
            DefaultValueFactory = (ar) => EnvironmentVariableParser.ParseBool(Environment.GetEnvironmentVariable("DOTNET_NOLOGO"), defaultValue),
            CustomParser = (ar) => true,
            Arity = ArgumentArity.Zero
        }.ForwardIfEnabled(forwardAs);
    }

    public static void ValidateSelfContainedOptions(bool hasSelfContainedOption, bool hasNoSelfContainedOption)
    {
        if (hasSelfContainedOption && hasNoSelfContainedOption)
        {
            throw new GracefulException(CommandDefinitionStrings.SelfContainAndNoSelfContainedConflict);
        }
    }

    /// <summary>
    /// Creates common diagnostics option (-d|--diagnostics).
    /// </summary>
    public static Option<bool> CreateDiagnosticsOption(bool recursive) => new("--diagnostics", "-d")
    {
        Description = CommandDefinitionStrings.SDKDiagnosticsCommandDefinition,
        Recursive = recursive,
        Arity = ArgumentArity.Zero
    };
}


