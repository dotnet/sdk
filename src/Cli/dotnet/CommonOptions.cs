// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Parsing;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli;

internal static class CommonOptions
{
    public static CliOption<string[]> PropertiesOption =
        // these are all of the forms that the property switch can be understood by in MSBuild
        new ForwardedOption<string[]>("--property", "-property", "/property", "/p", "-p", "--p")
        {
            Hidden = true
        }.ForwardAsProperty()
        .AllowSingleArgPerToken();

    public static CliOption<VerbosityOptions> VerbosityOption =
        new ForwardedOption<VerbosityOptions>("--verbosity", "-v")
        {
            Description = CommonLocalizableStrings.VerbosityOptionDescription,
            HelpName = CommonLocalizableStrings.LevelArgumentName
        }.ForwardAsSingle(o => $"-verbosity:{o}");

    public static CliOption<VerbosityOptions> HiddenVerbosityOption =
        new ForwardedOption<VerbosityOptions>("--verbosity", "-v")
        {
            Description = CommonLocalizableStrings.VerbosityOptionDescription,
            HelpName = CommonLocalizableStrings.LevelArgumentName,
            Hidden = true
        }.ForwardAsSingle(o => $"-verbosity:{o}");

    public static CliOption<string> FrameworkOption(string description) =>
        new DynamicForwardedOption<string>("--framework", "-f")
        {
            Description = description,
            HelpName = CommonLocalizableStrings.FrameworkArgumentName
        }
        .AddCompletions(Complete.TargetFrameworksFromProjectFile)
        .ForwardAsSingle(o => $"-property:TargetFramework={o}");

    public static CliOption<string> ArtifactsPathOption =
        new ForwardedOption<string>(
            //  --artifacts-path is pretty verbose, should we use --artifacts instead (or possibly support both)?
            "--artifacts-path")
        {
            Description = CommonLocalizableStrings.ArtifactsPathOptionDescription,
            HelpName = CommonLocalizableStrings.ArtifactsPathArgumentName
        }.ForwardAsSingle(o => $"-property:ArtifactsPath={CommandDirectoryContext.GetFullPath(o)}");

    private static string RuntimeArgName = CommonLocalizableStrings.RuntimeIdentifierArgumentName;
    public static IEnumerable<string> RuntimeArgFunc(string rid)
    {
        if (GetArchFromRid(rid) == "amd64")
        {
            rid = GetOsFromRid(rid) + "-x64";
        }
        return [$"-property:RuntimeIdentifier={rid}", "-property:_CommandLineDefinedRuntimeIdentifier=true"];
    }

    public static CliOption<string> RuntimeOption =
        new DynamicForwardedOption<string>("--runtime", "-r")
        {
            HelpName = RuntimeArgName
        }.ForwardAsMany(RuntimeArgFunc)
        .AddCompletions(Complete.RunTimesFromProjectFile);

    public static CliOption<string> LongFormRuntimeOption =
        new DynamicForwardedOption<string>("--runtime")
        {
            HelpName = RuntimeArgName
        }.ForwardAsMany(RuntimeArgFunc)
        .AddCompletions(Complete.RunTimesFromProjectFile);

    public static CliOption<bool> CurrentRuntimeOption(string description) =>
        new ForwardedOption<bool>("--use-current-runtime", "--ucr")
        {
            Description = description,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:UseCurrentRuntimeIdentifier=True");

    public static CliOption<string> ConfigurationOption(string description) =>
        new DynamicForwardedOption<string>("--configuration", "-c")
        {
            Description = description,
            HelpName = CommonLocalizableStrings.ConfigurationArgumentName
        }.ForwardAsSingle(o => $"-property:Configuration={o}")
        .AddCompletions(Complete.ConfigurationsFromProjectFileOrDefaults);

    public static CliOption<string> VersionSuffixOption =
        new ForwardedOption<string>("--version-suffix")
        {
            Description = CommonLocalizableStrings.CmdVersionSuffixDescription,
            HelpName = CommonLocalizableStrings.VersionSuffixArgumentName
        }.ForwardAsSingle(o => $"-property:VersionSuffix={o}");

    public static Lazy<string> NormalizedCurrentDirectory = new(() => PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()));

    public static CliArgument<string> DefaultToCurrentDirectory(this CliArgument<string> arg)
    {
        // we set this lazily so that we don't pay the overhead of determining the
        // CWD multiple times, one for each Argument that uses this.
        arg.DefaultValueFactory = _ => NormalizedCurrentDirectory.Value;
        return arg;
    }

    public static CliOption<bool> NoRestoreOption = new ForwardedOption<bool>("--no-restore")
    {
        Description = CommonLocalizableStrings.NoRestoreDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-restore:false");

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
             Description = CommonLocalizableStrings.CommandInteractiveOptionDescription,
             Arity = acceptArgument ? ArgumentArity.ZeroOrOne : ArgumentArity.Zero,
             // this default is called when no tokens/options are passed on the CLI args
             DefaultValueFactory = (ar) => IsCIEnvironmentOrRedirected()
         };

    public static CliOption<bool> InteractiveMsBuildForwardOption = InteractiveOption(acceptArgument: true).ForwardAsSingle(b => $"-property:NuGetInteractive={(b ? "true" : "false")}");

    public static CliOption<bool> DisableBuildServersOption =
        new ForwardedOption<bool>("--disable-build-servers")
        {
            Description = CommonLocalizableStrings.DisableBuildServersOptionDescription,
            Arity = ArgumentArity.Zero
        }
        .ForwardAsMany(_ => ["--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false"]);

    public static CliOption<string> ArchitectureOption =
        new ForwardedOption<string>("--arch", "-a")
        {
            Description = CommonLocalizableStrings.ArchitectureOptionDescription,
            HelpName = CommonLocalizableStrings.ArchArgumentName
        }.SetForwardingFunction(ResolveArchOptionToRuntimeIdentifier);

    public static CliOption<string> LongFormArchitectureOption =
        new ForwardedOption<string>("--arch")
        {
            Description = CommonLocalizableStrings.ArchitectureOptionDescription,
            HelpName = CommonLocalizableStrings.ArchArgumentName
        }.SetForwardingFunction(ResolveArchOptionToRuntimeIdentifier);

    internal static string ArchOptionValue(ParseResult parseResult) =>
        string.IsNullOrEmpty(parseResult.GetValue(ArchitectureOption)) ?
            parseResult.GetValue(LongFormArchitectureOption) :
            parseResult.GetValue(ArchitectureOption);

    public static CliOption<string> OperatingSystemOption =
        new ForwardedOption<string>("--os")
        {
            Description = CommonLocalizableStrings.OperatingSystemOptionDescription,
            HelpName = CommonLocalizableStrings.OSArgumentName
        }.SetForwardingFunction(ResolveOsOptionToRuntimeIdentifier);

    public static CliOption<bool> DebugOption = new("--debug")
    {
        Arity = ArgumentArity.Zero,
    };

    public static CliOption<bool> SelfContainedOption =
        new ForwardedOption<bool>("--self-contained", "--sc")
        {
            Description = CommonLocalizableStrings.SelfContainedOptionDescription
        }
        .SetForwardingFunction(ForwardSelfContainedOptions);

    public static CliOption<bool> NoSelfContainedOption =
        new ForwardedOption<bool>("--no-self-contained")
        {
            Description = CommonLocalizableStrings.FrameworkDependentOptionDescription,
            Arity = ArgumentArity.Zero
        }
        .SetForwardingFunction((_, p) => ForwardSelfContainedOptions(false, p));

    public static readonly CliOption<IReadOnlyDictionary<string, string>> EnvOption = new("--environment", "-e")
    {
        Description = CommonLocalizableStrings.CmdEnvironmentVariableDescription,
        HelpName = CommonLocalizableStrings.CmdEnvironmentVariableExpression,
        CustomParser = ParseEnvironmentVariables,
        // Can't allow multiple arguments because the separator needs to be parsed as part of the environment variable value.
        AllowMultipleArgumentsPerToken = false
    };

    private static IReadOnlyDictionary<string, string> ParseEnvironmentVariables(ArgumentResult argumentResult)
    {
        var result = new Dictionary<string, string>(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        List<CliToken>? invalid = null;

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
                CommonLocalizableStrings.IncorrectlyFormattedEnvironmentVariables,
                string.Join(", ", invalid.Select(x => $"'{x.Value}'"))));
        }

        return result;
    }

    public static readonly CliOption<string> TestPlatformOption = new("--Platform");

    public static readonly CliOption<string> TestFrameworkOption = new("--Framework");

    public static readonly CliOption<string[]> TestLoggerOption = new("--logger");

    public static void ValidateSelfContainedOptions(bool hasSelfContainedOption, bool hasNoSelfContainedOption)
    {
        if (hasSelfContainedOption && hasNoSelfContainedOption)
        {
            throw new GracefulException(CommonLocalizableStrings.SelfContainAndNoSelfContainedConflict);
        }
    }

    internal static IEnumerable<string> ResolveArchOptionToRuntimeIdentifier(string arg, ParseResult parseResult)
    {
        if ((parseResult.GetResult(RuntimeOption) ?? parseResult.GetResult(LongFormRuntimeOption)) is not null)
        {
            throw new GracefulException(CommonLocalizableStrings.CannotSpecifyBothRuntimeAndArchOptions);
        }

        if (parseResult.BothArchAndOsOptionsSpecified())
        {
            // ResolveOsOptionToRuntimeIdentifier handles resolving the RID when both arch and os are specified
            return Array.Empty<string>();
        }

        return ResolveRidShorthandOptions(null, arg);
    }

    internal static IEnumerable<string> ResolveOsOptionToRuntimeIdentifier(string arg, ParseResult parseResult)
    {
        if ((parseResult.GetResult(RuntimeOption) ?? parseResult.GetResult(LongFormRuntimeOption)) is not null)
        {
            throw new GracefulException(CommonLocalizableStrings.CannotSpecifyBothRuntimeAndOsOptions);
        }

        var arch = parseResult.BothArchAndOsOptionsSpecified() ? ArchOptionValue(parseResult) : null;
        return ResolveRidShorthandOptions(arg, arch);
    }

    private static IEnumerable<string> ResolveRidShorthandOptions(string os, string arch) =>
        [$"-property:RuntimeIdentifier={ResolveRidShorthandOptionsToRuntimeIdentifier(os, arch)}"];

    internal static string ResolveRidShorthandOptionsToRuntimeIdentifier(string os, string arch)
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
        string dotnetRootPath = NativeWrapper.EnvironmentProvider.GetDotnetExeDirectory(key =>
            key.Equals("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", StringComparison.InvariantCultureIgnoreCase)
                ? null
                : Environment.GetEnvironmentVariable(key));
        var ridFileName = "NETCoreSdkRuntimeIdentifierChain.txt";
        // When running under test the Product.Version might be empty or point to version not installed in dotnetRootPath.
        string runtimeIdentifierChainPath = string.IsNullOrEmpty(Product.Version) || !Directory.Exists(Path.Combine(dotnetRootPath, "sdk", Product.Version)) ?
            Path.Combine(Directory.GetDirectories(Path.Combine(dotnetRootPath, "sdk"))[0], ridFileName) :
            Path.Combine(dotnetRootPath, "sdk", Product.Version, ridFileName);
        string[] currentRuntimeIdentifiers = File.Exists(runtimeIdentifierChainPath) ? File.ReadAllLines(runtimeIdentifierChainPath).Where(l => !string.IsNullOrEmpty(l)).ToArray() : [];
        if (currentRuntimeIdentifiers == null || !currentRuntimeIdentifiers.Any() || !currentRuntimeIdentifiers[0].Contains("-"))
        {
            throw new GracefulException(CommonLocalizableStrings.CannotResolveRuntimeIdentifier);
        }
        return currentRuntimeIdentifiers[0]; // First rid is the most specific (ex win-x64)
    }

    private static string GetOsFromRid(string rid) => rid.Substring(0, rid.LastIndexOf("-", StringComparison.InvariantCulture));

    private static string GetArchFromRid(string rid) => rid.Substring(rid.LastIndexOf("-", StringComparison.InvariantCulture) + 1, rid.Length - rid.LastIndexOf("-", StringComparison.InvariantCulture) - 1);

    private static IEnumerable<string> ForwardSelfContainedOptions(bool isSelfContained, ParseResult parseResult)
    {
        IEnumerable<string> selfContainedProperties = [$"-property:SelfContained={isSelfContained}", "-property:_CommandLineDefinedSelfContained=true"];
        return selfContainedProperties;
    }

    internal static CliOption<T> AddCompletions<T>(this CliOption<T> option, Func<CompletionContext, IEnumerable<CompletionItem>> completionSource)
    {
        option.CompletionSources.Add(completionSource);
        return option;
    }

    internal static CliArgument<T> AddCompletions<T>(this CliArgument<T> argument, Func<CompletionContext, IEnumerable<CompletionItem>> completionSource)
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

public enum VerbosityOptions
{
    quiet,
    q,
    minimal,
    m,
    normal,
    n,
    detailed,
    d,
    diagnostic,
    diag
}

public class DynamicOption<T>(string name, params string[] aliases) : CliOption<T>(name, aliases), IDynamicOption
{
}

public class DynamicArgument<T>(string name) : CliArgument<T>(name), IDynamicArgument
{
}
