// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using System.CommandLine.StaticCompletions;

namespace Microsoft.DotNet.Cli;

internal readonly struct TargetPlatformOptions
{
    public const string RuntimeOptionName = "--runtime";

    public readonly Option RuntimeOption;

    public readonly Option<string> ArchitectureOption = new("--arch", "-a")
    {
        Description = CliStrings.ArchitectureOptionDescription,
        HelpName = CliStrings.ArchArgumentName
    };

    public readonly Option<string> OperatingSystemOption = new("--os")
    {
        Description = CliStrings.OperatingSystemOptionDescription,
        HelpName = CliStrings.OSArgumentName
    };

    public TargetPlatformOptions(Option runtimeOption)
    {
        RuntimeOption = runtimeOption;
        ArchitectureOption.SetForwardingFunction(ResolveArchOptionToRuntimeIdentifier);
        OperatingSystemOption.SetForwardingFunction(ResolveOsOptionToRuntimeIdentifier);
    }

    public TargetPlatformOptions(string runtimeOptionDescription)
        : this(CreateRuntimeOption(runtimeOptionDescription))
    {
    }

    public void AddTo(IList<Option> options)
    {
        options.Add(RuntimeOption);
        options.Add(ArchitectureOption);
        options.Add(OperatingSystemOption);
    }

    public static IEnumerable<string> RuntimeArgFunc(string rid)
    {
        if (GetArchFromRid(rid) == "amd64")
        {
            rid = GetOsFromRid(rid) + "-x64";
        }
        return [$"--property:RuntimeIdentifier={rid}", "--property:_CommandLineDefinedRuntimeIdentifier=true"];
    }

    public static Option<string> CreateRuntimeOption(string description) =>
        new Option<string>(RuntimeOptionName, "-r")
        {
            HelpName = CliStrings.RuntimeIdentifierArgumentName,
            Description = description,
            IsDynamic = true
        }.ForwardAsMany(RuntimeArgFunc!);

    public IEnumerable<string> ResolveArchOptionToRuntimeIdentifier(string? arg, ParseResult parseResult)
    {
        if (parseResult.HasOption(RuntimeOption))
        {
            throw new GracefulException(CliStrings.CannotSpecifyBothRuntimeAndArchOptions);
        }

        if (parseResult.HasOption(ArchitectureOption) && parseResult.HasOption(OperatingSystemOption))
        {
            // ResolveOsOptionToRuntimeIdentifier handles resolving the RID when both arch and os are specified
            return [];
        }

        return ResolveRidShorthandOptions(os: null, arch: arg);
    }

    public IEnumerable<string> ResolveOsOptionToRuntimeIdentifier(string? arg, ParseResult parseResult)
    {
        if (parseResult.HasOption(RuntimeOption))
        {
            throw new GracefulException(CliStrings.CannotSpecifyBothRuntimeAndOsOptions);
        }

        var arch = parseResult.HasOption(ArchitectureOption) && parseResult.HasOption(OperatingSystemOption)
            ? parseResult.GetValue(ArchitectureOption)
            : null;

        return ResolveRidShorthandOptions(arg, arch);
    }

    private static IEnumerable<string> ResolveRidShorthandOptions(string? os, string? arch) =>
        [$"--property:RuntimeIdentifier={ResolveRidShorthandOptionsToRuntimeIdentifier(os, arch)}"];

    public static string ResolveRidShorthandOptionsToRuntimeIdentifier(string? os, string? arch)
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

    private static string GetOsFromRid(string rid)
        => rid.Substring(0, rid.LastIndexOf("-", StringComparison.InvariantCulture));

    private static string GetArchFromRid(string rid)
        => rid.Substring(rid.LastIndexOf("-", StringComparison.InvariantCulture) + 1, rid.Length - rid.LastIndexOf("-", StringComparison.InvariantCulture) - 1);
}


