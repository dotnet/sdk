// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Parsing;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli;

internal static class CommonOptions
{
    public static Option<bool> YesOption =
        new DynamicOption<bool>("--yes", "-y")
        {
            Description = CliStrings.YesOptionDescription,
            Arity = ArgumentArity.Zero
        };

    public static Option<ReadOnlyDictionary<string, string>?> PropertiesOption =
        // these are all of the forms that the property switch can be understood by in MSBuild
        new ForwardedOption<ReadOnlyDictionary<string, string>?>("--property", "-property", "/property", "/p", "-p", "--p")
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
        new ForwardedOption<ReadOnlyDictionary<string, string>?>("--restoreProperty", "-restoreProperty", "/restoreProperty", "-rp", "--rp", "/rp")
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
        new ForwardedOption<string[]?>("--target", "/target", "-target", "-t", "--t", "/t")
        {
            Description = "Build these targets in this project. Use a semicolon or a comma to separate multiple targets, or specify each target separately.",
            HelpName = "TARGET",
            DefaultValueFactory = _ => defaultTargetName is not null ? [defaultTargetName] : null,
            CustomParser = r => SplitMSBuildTargets(defaultTargetName, r),
            Hidden = true,
            Arity = ArgumentArity.ZeroOrMore
        }
        .ForwardAsMany(targets => ForwardTargetsAndAdditionalProperties(targets, additionalProperties))
        .AllowSingleArgPerToken();


    public static Option<string[]> RequiredMSBuildTargetOption(string defaultTargetName, (string key, string value)[]? additionalProperties = null) =>
        new ForwardedOption<string[]>("--target", "/target", "-target", "-t", "--t", "/t")
        {
            Description = "Build these targets in this project. Use a semicolon or a comma to separate multiple targets, or specify each target separately.",
            HelpName = "TARGET",
            DefaultValueFactory = _ => [defaultTargetName],
            CustomParser = r => SplitMSBuildTargets(defaultTargetName, r),
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

    public static string[] SplitMSBuildTargets(string? defaultTargetName, ArgumentResult argumentResult)
    {
        if (argumentResult.Tokens.Count == 0)
        {
            return defaultTargetName is not null ? [defaultTargetName] : [];
        }
        var userTargets =
            argumentResult.Tokens.Select(t => t.Value)
            .SelectMany(t => t.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(t => !string.IsNullOrEmpty(t));
        var allTargets = defaultTargetName is null ? userTargets : [defaultTargetName, .. userTargets];
        return allTargets.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static Option<VerbosityOptions> VerbosityOption(VerbosityOptions defaultVerbosity) =>
        new ForwardedOption<VerbosityOptions>("--verbosity", "-v")
        {
            Description = CliStrings.VerbosityOptionDescription,
            HelpName = CliStrings.LevelArgumentName,
            DefaultValueFactory = _ => defaultVerbosity
        }
        .ForwardAsSingle(o => $"-verbosity:{o}")
        .AggregateRepeatedTokens();

    public static Option<VerbosityOptions?> VerbosityOption() =>
        new ForwardedOption<VerbosityOptions?>("--verbosity", "-v", "--v", "-verbosity", "/v", "/verbosity")
        {
            Description = CliStrings.VerbosityOptionDescription,
            HelpName = CliStrings.LevelArgumentName
        }
        .ForwardAsSingle(o => $"--verbosity:{o}")
        .AggregateRepeatedTokens();

    public static Option<VerbosityOptions> HiddenVerbosityOption =
        new ForwardedOption<VerbosityOptions>("--verbosity", "-v", "--v", "-verbosity", "/v", "/verbosity")
        {
            Description = CliStrings.VerbosityOptionDescription,
            HelpName = CliStrings.LevelArgumentName,
            Hidden = true
        }
        .ForwardAsSingle(o => $"--verbosity:{o}")
        .AggregateRepeatedTokens();

    public static Option<string> FrameworkOption(string description) =>
        new DynamicForwardedOption<string>("--framework", "-f")
        {
            Description = description,
            HelpName = CliStrings.FrameworkArgumentName
        }
        .AddCompletions(CliCompletion.TargetFrameworksFromProjectFile)
        .ForwardAsSingle(o => $"--property:TargetFramework={o}");

    public static Option<string> ArtifactsPathOption =
        new ForwardedOption<string>(
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
        new DynamicForwardedOption<string>(RuntimeOptionName, "-r")
        {
            HelpName = RuntimeArgName,
            Description = description
        }.ForwardAsMany(RuntimeArgFunc!)
        .AddCompletions(CliCompletion.RunTimesFromProjectFile);

    public static Option<string> LongFormRuntimeOption =
        new DynamicForwardedOption<string>(RuntimeOptionName)
        {
            HelpName = RuntimeArgName
        }.ForwardAsMany(RuntimeArgFunc!)
        .AddCompletions(CliCompletion.RunTimesFromProjectFile);

    public static Option<bool> CurrentRuntimeOption(string description) =>
        new ForwardedOption<bool>("--use-current-runtime", "--ucr")
        {
            Description = description,
            Arity = ArgumentArity.Zero
        }.ForwardAs("--property:UseCurrentRuntimeIdentifier=True");

    public static Option<string?> ConfigurationOption(string description) =>
        new DynamicForwardedOption<string?>("--configuration", "-c")
        {
            Description = description,
            HelpName = CliStrings.ConfigurationArgumentName
        }.ForwardAsSingle(o => $"--property:Configuration={o}")
        .AddCompletions(CliCompletion.ConfigurationsFromProjectFileOrDefaults);

    public static Option<string> VersionSuffixOption =
        new ForwardedOption<string>("--version-suffix")
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

    public static Option<bool> NoRestoreOption = new ForwardedOption<bool>("--no-restore")
    {
        Description = CliStrings.NoRestoreDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-restore:false");


    public static Option<bool> RestoreOption = new ForwardedOption<bool>("--restore", "-restore")
    {
        Description = "Restore the project before building it. This is the default behavior.",
        Arity = ArgumentArity.Zero,
        Hidden = true
    }.ForwardAs("-restore");

    private static bool IsCIEnvironmentOrRedirected() =>
        new Telemetry.CIEnvironmentDetectorForTelemetry().IsCIEnvironment() || Console.IsOutputRedirected;

    /// <summary>
    /// A 'template' for interactive usage across the whole dotnet CLI. Use this as a base and then specialize it for your use cases.
    /// Despite being a 'forwarded option' there is no default forwarding configured, so if you want forwarding you can add it on a per-command basis.
    /// </summary>
    /// <param name="acceptArgument">Whether the option accepts an boolean argument. If false, the option will be a flag.</param>
    /// <remarks>
    // If not set by a user, this will default to true if the user is not in a CI environment as detected by <see cref="Telemetry.CIEnvironmentDetectorForTelemetry.IsCIEnvironment"/>.
    // If this is set to function as a flag, then there is no simple user-provided way to circumvent the behavior.
    // </remarks>
    public static ForwardedOption<bool> InteractiveOption(bool acceptArgument = false) =>
         new("--interactive")
         {
             Description = CliStrings.CommandInteractiveOptionDescription,
             Arity = acceptArgument ? ArgumentArity.ZeroOrOne : ArgumentArity.Zero,
             // this default is called when no tokens/options are passed on the CLI args
             DefaultValueFactory = (ar) => !IsCIEnvironmentOrRedirected()
         };

    public static Option<bool> InteractiveMsBuildForwardOption = InteractiveOption(acceptArgument: true).ForwardAsSingle(b => $"--property:NuGetInteractive={(b ? "true" : "false")}");

    public static Option<bool> DisableBuildServersOption =
        new ForwardedOption<bool>("--disable-build-servers")
        {
            Description = CliStrings.DisableBuildServersOptionDescription,
            Arity = ArgumentArity.Zero
        }
        .ForwardAsMany(_ => ["--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false"]);

    public static Option<string> ArchitectureOption =
        new ForwardedOption<string>("--arch", "-a")
        {
            Description = CliStrings.ArchitectureOptionDescription,
            HelpName = CliStrings.ArchArgumentName
        }.SetForwardingFunction(ResolveArchOptionToRuntimeIdentifier);

    public static Option<string> LongFormArchitectureOption =
        new ForwardedOption<string>("--arch")
        {
            Description = CliStrings.ArchitectureOptionDescription,
            HelpName = CliStrings.ArchArgumentName
        }.SetForwardingFunction(ResolveArchOptionToRuntimeIdentifier);

    internal static string? ArchOptionValue(ParseResult parseResult) =>
        string.IsNullOrEmpty(parseResult.GetValue(ArchitectureOption)) ?
            parseResult.GetValue(LongFormArchitectureOption) :
            parseResult.GetValue(ArchitectureOption);

    public static Option<string> OperatingSystemOption =
        new ForwardedOption<string>("--os")
        {
            Description = CliStrings.OperatingSystemOptionDescription,
            HelpName = CliStrings.OSArgumentName
        }.SetForwardingFunction(ResolveOsOptionToRuntimeIdentifier);

    public static Option<bool> DebugOption = new("--debug")
    {
        Arity = ArgumentArity.Zero,
    };

    public static Option<bool> SelfContainedOption =
        new ForwardedOption<bool>("--self-contained", "--sc")
        {
            Description = CliStrings.SelfContainedOptionDescription
        }
        .SetForwardingFunction(ForwardSelfContainedOptions);

    public static Option<bool> NoSelfContainedOption =
        new ForwardedOption<bool>("--no-self-contained")
        {
            Description = CliStrings.FrameworkDependentOptionDescription,
            Arity = ArgumentArity.Zero
        }
        .SetForwardingFunction((_, p) => ForwardSelfContainedOptions(false, p));

    public static readonly Option<IReadOnlyDictionary<string, string>> EnvOption = new("--environment", "-e")
    {
        Description = CliStrings.CmdEnvironmentVariableDescription,
        HelpName = CliStrings.CmdEnvironmentVariableExpression,
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
                CliStrings.IncorrectlyFormattedEnvironmentVariables,
                string.Join(", ", invalid.Select(x => $"'{x.Value}'"))));
        }

        return result;
    }

    public static readonly Option<string> TestPlatformOption = new("--Platform");

    public static readonly Option<string> TestFrameworkOption = new("--Framework");

    public static readonly Option<string[]> TestLoggerOption = new("--logger");

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

    private static IEnumerable<string> ForwardSelfContainedOptions(bool isSelfContained, ParseResult parseResult)
    {
        IEnumerable<string> selfContainedProperties = [$"--property:SelfContained={isSelfContained}", "--property:_CommandLineDefinedSelfContained=true"];
        return selfContainedProperties;
    }

    internal static Option<T> AddCompletions<T>(this Option<T> option, Func<CompletionContext, IEnumerable<CompletionItem>> completionSource)
    {
        option.CompletionSources.Add(completionSource);
        return option;
    }

    internal static Argument<T> AddCompletions<T>(this Argument<T> argument, Func<CompletionContext, IEnumerable<CompletionItem>> completionSource)
    {
        argument.CompletionSources.Add(completionSource);
        return argument;
    }

    internal static DynamicOption<T> AddCompletions<T>(this DynamicOption<T> option, Func<CompletionContext, IEnumerable<CompletionItem>> completionSource)
    {
        option.CompletionSources.Add(completionSource);
        return option;
    }

    internal static DynamicForwardedOption<T> AddCompletions<T>(this DynamicForwardedOption<T> option, Func<CompletionContext, IEnumerable<CompletionItem>> completionSource)
    {
        option.CompletionSources.Add(completionSource);
        return option;
    }
}

public class DynamicOption<T>(string name, params string[] aliases) : Option<T>(name, aliases), IDynamicOption
{
}

public class DynamicArgument<T>(string name) : Argument<T>(name), IDynamicArgument
{
}
