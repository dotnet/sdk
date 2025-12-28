// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Parsing;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli;

internal static class CommonOptions
{
    public static Option<bool> CreateYesOption() => new("--yes", "-y")
    {
        Description = CliStrings.YesOptionDescription,
        Arity = ArgumentArity.Zero,
        IsDynamic = true
    };

    public static Option<ReadOnlyDictionary<string, string>?> PropertiesOption =
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
    public static Option<ReadOnlyDictionary<string, string>?> RestorePropertiesOption =
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

    public static Option<string[]?> MSBuildTargetOption(string? defaultTargetName = null, (string key, string value)[]? additionalProperties = null) =>
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

    public static Option<string[]> RequiredMSBuildTargetOption(string defaultTargetName, (string key, string value)[]? additionalProperties = null) =>
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

    public static readonly Option<string[]?> GetPropertyOption = MSBuildMultiOption("getProperty");

    public static readonly Option<string[]?> GetItemOption = MSBuildMultiOption("getItem");

    public static readonly Option<string[]?> GetTargetResultOption = MSBuildMultiOption("getTargetResult");

    public static readonly Option<string[]?> GetResultOutputFileOption = MSBuildMultiOption("getResultOutputFile");

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
            Description = CliStrings.VerbosityOptionDescription,
            HelpName = CliStrings.LevelArgumentName,
            DefaultValueFactory = _ => defaultVerbosity
        }
        .ForwardAsSingle(o => $"--verbosity:{o}")
        .AggregateRepeatedTokens();

    public static Option<VerbosityOptions?> CreateVerbosityOption() =>
        new Option<VerbosityOptions?>("--verbosity", "-v", "--v", "-verbosity", "/v", "/verbosity")
        {
            Description = CliStrings.VerbosityOptionDescription,
            HelpName = CliStrings.LevelArgumentName
        }
        .ForwardAsSingle(o => $"--verbosity:{o}")
        .AggregateRepeatedTokens();

    public static Option<VerbosityOptions> CreateHiddenVerbosityOption() =>
        new Option<VerbosityOptions>("--verbosity", "-v", "--v", "-verbosity", "/v", "/verbosity")
        {
            Description = CliStrings.VerbosityOptionDescription,
            HelpName = CliStrings.LevelArgumentName,
            Hidden = true
        }
        .ForwardAsSingle(o => $"--verbosity:{o}")
        .AggregateRepeatedTokens();

    public static Option<string> FrameworkOption(string description) =>
        new Option<string>("--framework", "-f")
        {
            Description = description,
            HelpName = CliStrings.FrameworkArgumentName,
            IsDynamic = true
        }
        .ForwardAsSingle(o => $"--property:TargetFramework={o}")
        .AddCompletions(CliCompletion.TargetFrameworksFromProjectFile);

    public static Option<string> ArtifactsPathOption =
        new Option<string>(
            //  --artifacts-path is pretty verbose, should we use --artifacts instead (or possibly support both)?
            "--artifacts-path")
        {
            Description = CliStrings.ArtifactsPathOptionDescription,
            HelpName = CliStrings.ArtifactsPathArgumentName
        }.ForwardAsSingle(o => $"--property:ArtifactsPath={CommandDirectoryContext.GetFullPath(o)}");

    private static readonly string RuntimeArgName = CliStrings.RuntimeIdentifierArgumentName;
    public static IEnumerable<string> RuntimeArgFunc(string rid)
    {
        if (GetArchFromRid(rid) == "amd64")
        {
            rid = GetOsFromRid(rid) + "-x64";
        }
        return [$"--property:RuntimeIdentifier={rid}", "--property:_CommandLineDefinedRuntimeIdentifier=true"];
    }

    public const string RuntimeOptionName = "--runtime";

    public static Option<string> RuntimeOption(string description) =>
        new Option<string>(RuntimeOptionName, "-r")
        {
            HelpName = RuntimeArgName,
            Description = description,
            IsDynamic = true
        }.ForwardAsMany(RuntimeArgFunc!)
        .AddCompletions(CliCompletion.RunTimesFromProjectFile);

    public static Option<string> LongFormRuntimeOption =
        new Option<string>(RuntimeOptionName)
        {
            HelpName = RuntimeArgName,
            IsDynamic = true,
        }.ForwardAsMany(RuntimeArgFunc!)
        .AddCompletions(CliCompletion.RunTimesFromProjectFile);

    public static Option<bool> CurrentRuntimeOption(string description) =>
        new Option<bool>("--use-current-runtime", "--ucr")
        {
            Description = description,
            Arity = ArgumentArity.Zero
        }.ForwardAs("--property:UseCurrentRuntimeIdentifier=True");

    public static Option<string?> ConfigurationOption(string description) =>
        new Option<string?>("--configuration", "-c")
        {
            Description = description,
            HelpName = CliStrings.ConfigurationArgumentName,
            IsDynamic = true
        }.ForwardAsSingle(o => $"--property:Configuration={o}")
        .AddCompletions(CliCompletion.ConfigurationsFromProjectFileOrDefaults);

    public static Option<string> VersionSuffixOption =
        new Option<string>("--version-suffix")
        {
            Description = CliStrings.CmdVersionSuffixDescription,
            HelpName = CliStrings.VersionSuffixArgumentName
        }.ForwardAsSingle(o => $"--property:VersionSuffix={o}");

    public static Lazy<string> NormalizedCurrentDirectory = new(() => PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()));

    public static Argument<string> DefaultToCurrentDirectory(this Argument<string> arg)
    {
        // we set this lazily so that we don't pay the overhead of determining the
        // CWD multiple times, one for each Argument that uses this.
        arg.DefaultValueFactory = _ => NormalizedCurrentDirectory.Value;
        return arg;
    }

    public static Option<bool> NoRestoreOption = new Option<bool>("--no-restore")
    {
        Description = CliStrings.NoRestoreDescription,
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
            Description = CliStrings.CommandInteractiveOptionDescription,
            Arity = acceptArgument ? ArgumentArity.ZeroOrOne : ArgumentArity.Zero,
            // this default is called when no tokens/options are passed on the CLI args
            DefaultValueFactory = (ar) => !IsCIEnvironmentOrRedirected(),
            Hidden = hidden,
        };

    public static Option<bool> InteractiveMsBuildForwardOption = CreateInteractiveOption(acceptArgument: true).ForwardAsSingle(b => $"--property:NuGetInteractive={(b ? "true" : "false")}");

    public static Option<bool> DisableBuildServersOption =
        new Option<bool>("--disable-build-servers")
        {
            Description = CliStrings.DisableBuildServersOptionDescription,
            Arity = ArgumentArity.Zero
        }
        .ForwardIfEnabled(["--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false"]);

    public static Option<string> ArchitectureOption =
        new Option<string>("--arch", "-a")
        {
            Description = CliStrings.ArchitectureOptionDescription,
            HelpName = CliStrings.ArchArgumentName
        }.SetForwardingFunction(ResolveArchOptionToRuntimeIdentifier);

    public static Option<string> LongFormArchitectureOption =
        new Option<string>("--arch")
        {
            Description = CliStrings.ArchitectureOptionDescription,
            HelpName = CliStrings.ArchArgumentName
        }.SetForwardingFunction(ResolveArchOptionToRuntimeIdentifier);

    internal static string? ArchOptionValue(ParseResult parseResult) =>
        string.IsNullOrEmpty(parseResult.GetValue(ArchitectureOption)) ?
            parseResult.GetValue(LongFormArchitectureOption) :
            parseResult.GetValue(ArchitectureOption);

    public static Option<string> OperatingSystemOption =
        new Option<string>("--os")
        {
            Description = CliStrings.OperatingSystemOptionDescription,
            HelpName = CliStrings.OSArgumentName
        }.SetForwardingFunction(ResolveOsOptionToRuntimeIdentifier);

    public static Option<bool> DebugOption = new("--debug")
    {
        Arity = ArgumentArity.Zero,
    };

    public static Option<bool> SelfContainedOption =
        new Option<bool>("--self-contained", "--sc")
        {
            Description = CliStrings.SelfContainedOptionDescription
        }
        .ForwardIfEnabled([$"--property:SelfContained=true", "--property:_CommandLineDefinedSelfContained=true"]);

    public static Option<bool> NoSelfContainedOption =
        new Option<bool>("--no-self-contained")
        {
            Description = CliStrings.FrameworkDependentOptionDescription,
            Arity = ArgumentArity.Zero
        }
        .ForwardIfEnabled([$"--property:SelfContained=false", "--property:_CommandLineDefinedSelfContained=true"]);

    public static Option<IReadOnlyDictionary<string, string>> CreateEnvOption(string description) => new("--environment", "-e")
    {
        Description = description,
        HelpName = CliStrings.CmdEnvironmentVariableExpression,
        CustomParser = ParseEnvironmentVariables,
        // Can't allow multiple arguments because the separator needs to be parsed as part of the environment variable value.
        AllowMultipleArgumentsPerToken = false
    };

    public static readonly Option<IReadOnlyDictionary<string, string>> EnvOption = CreateEnvOption(CliStrings.CmdEnvironmentVariableDescription);
    
    public static readonly Option<IReadOnlyDictionary<string, string>> TestEnvOption = CreateEnvOption(CliStrings.CmdTestEnvironmentVariableDescription);

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
                CliStrings.IncorrectlyFormattedEnvironmentVariables,
                string.Join(", ", invalid.Select(x => $"'{x.Value}'"))));
        }

        return result;
    }

    public static readonly Option<string> TestPlatformOption = new("--Platform");

    public static readonly Option<string> TestFrameworkOption = new("--Framework");

    public static readonly Option<string[]> TestLoggerOption = new("--logger");

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
    public static Option<bool> NoLogoOption(bool defaultValue = true, string forwardAs = "--nologo", string? description = null)
    {
        return new Option<bool>("--no-logo", "--nologo", "-nologo", "/nologo")
        {
            Description = description ?? Commands.CliCommandStrings.NoLogoOptionDescription,
            DefaultValueFactory = (ar) => Env.TryGetEnvironmentVariableAsBool("DOTNET_NOLOGO", out bool value) ? value : defaultValue,
            CustomParser = (ar) => true,
            Arity = ArgumentArity.Zero
        }.ForwardIfEnabled(forwardAs);
    }

    public static void ValidateSelfContainedOptions(bool hasSelfContainedOption, bool hasNoSelfContainedOption)
    {
        if (hasSelfContainedOption && hasNoSelfContainedOption)
        {
            throw new GracefulException(CliStrings.SelfContainAndNoSelfContainedConflict);
        }
    }

    internal static IEnumerable<string> ResolveArchOptionToRuntimeIdentifier(string? arg, ParseResult parseResult)
    {
        if (parseResult.GetResult(RuntimeOptionName) is not null)
        {
            throw new GracefulException(CliStrings.CannotSpecifyBothRuntimeAndArchOptions);
        }

        if (parseResult.BothArchAndOsOptionsSpecified())
        {
            // ResolveOsOptionToRuntimeIdentifier handles resolving the RID when both arch and os are specified
            return [];
        }

        return ResolveRidShorthandOptions(null, arg);
    }

    internal static IEnumerable<string> ResolveOsOptionToRuntimeIdentifier(string? arg, ParseResult parseResult)
    {
        if (parseResult.GetResult(RuntimeOptionName) is not null)
        {
            throw new GracefulException(CliStrings.CannotSpecifyBothRuntimeAndOsOptions);
        }

        var arch = parseResult.BothArchAndOsOptionsSpecified() ? ArchOptionValue(parseResult) : null;
        return ResolveRidShorthandOptions(arg, arch);
    }

    private static IEnumerable<string> ResolveRidShorthandOptions(string? os, string? arch) =>
        [$"--property:RuntimeIdentifier={ResolveRidShorthandOptionsToRuntimeIdentifier(os, arch)}"];

    internal static string ResolveRidShorthandOptionsToRuntimeIdentifier(string? os, string? arch)
    {
        var currentRid = GetCurrentRuntimeId();
        arch = arch == "amd64" ? "x64" : arch;
        os = string.IsNullOrEmpty(os) ? GetOsFromRid(currentRid) : os;
        arch = string.IsNullOrEmpty(arch) ? GetArchFromRid(currentRid) : arch;
        return $"{os}-{arch}";
    }

    public static string GetCurrentRuntimeId()
    {
        // Get the dotnet directory, while ignoring custom msbuild resolvers
        string? dotnetRootPath = NativeWrapper.EnvironmentProvider.GetDotnetExeDirectory(key =>
            key.Equals("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", StringComparison.InvariantCultureIgnoreCase)
                ? null
                : Environment.GetEnvironmentVariable(key));
        var ridFileName = "NETCoreSdkRuntimeIdentifierChain.txt";
        var sdkPath = dotnetRootPath is not null ? Path.Combine(dotnetRootPath, "sdk") : "sdk";

        // When running under test the Product.Version might be empty or point to version not installed in dotnetRootPath.
        string runtimeIdentifierChainPath = string.IsNullOrEmpty(Product.Version) || !Directory.Exists(Path.Combine(sdkPath, Product.Version)) ?
            Path.Combine(Directory.GetDirectories(sdkPath)[0], ridFileName) :
            Path.Combine(sdkPath, Product.Version, ridFileName);
        string[] currentRuntimeIdentifiers = File.Exists(runtimeIdentifierChainPath) ? [.. File.ReadAllLines(runtimeIdentifierChainPath).Where(l => !string.IsNullOrEmpty(l))] : [];
        if (currentRuntimeIdentifiers == null || !currentRuntimeIdentifiers.Any() || !currentRuntimeIdentifiers[0].Contains("-"))
        {
            throw new GracefulException(CliStrings.CannotResolveRuntimeIdentifier);
        }
        return currentRuntimeIdentifiers[0]; // First rid is the most specific (ex win-x64)
    }

    private static string GetOsFromRid(string rid) => rid.Substring(0, rid.LastIndexOf("-", StringComparison.InvariantCulture));

    private static string GetArchFromRid(string rid) => rid.Substring(rid.LastIndexOf("-", StringComparison.InvariantCulture) + 1, rid.Length - rid.LastIndexOf("-", StringComparison.InvariantCulture) - 1);
}


