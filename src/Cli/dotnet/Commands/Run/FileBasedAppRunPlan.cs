// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Computes shared managed and Native AOT build and launch decisions for file-based applications.
/// </summary>
internal static class FileBasedAppRunPlan
{
    /// <summary>The marker written when a file-based application build starts.</summary>
    internal const string BuildStartCacheFileName = "build-start.cache";

    /// <summary>The cache entry written after a successful file-based application build.</summary>
    internal const string BuildSuccessCacheFileName = "build-success.cache";

    private static readonly ImmutableArray<(string Name, bool IsMSBuildFile)> s_implicitBuildFiles =
    [
        ("global.json", false),

        // NuGet recognizes all these casings on case-sensitive platforms.
        ("nuget.config", false),
        ("NuGet.config", false),
        ("NuGet.Config", false),

        ("Directory.Build.props", true),
        ("Directory.Build.targets", true),
        ("Directory.Packages.props", true),
        ("Directory.Build.rsp", true),
        ("MSBuild.rsp", true),
    ];

    private static readonly IEnumerable<string> s_ignorableProperties =
    [
        // These are set by default by dotnet run and do not affect the build on their own.
        "NuGetInteractive",
        "_BuildNonexistentProjectsByDefault",
        "RestoreUseSkipNonexistentTargets",
        "ProvideCommandLineArgs",
    ];

    /// <summary>
    /// Computes the build level required by the current file-based application inputs.
    /// </summary>
    /// <param name="inputs">The current planning inputs.</param>
    /// <param name="report">Receives planning diagnostics.</param>
    /// <returns>The selected run plan.</returns>
    internal static RunPlan Analyze(
        FileBasedAppRunPlanInputs inputs,
        Action<string> report)
    {
        if (inputs.DirectiveInfo.ProbeResult == FileBasedAppDirectiveProbeResult.Unknown)
        {
            report("Deferring to the managed CLI because the source may contain file directives.");
            return new RunPlan(RunTier.ManagedFallback, RunDecisionReason.DirectiveProbeUnknown, Cache: null);
        }

        BuildLevel buildLevel = AnalyzeBuildLevel(inputs, report, out FileBasedAppCacheInfo? cache);
        return buildLevel switch
        {
            BuildLevel.None => new RunPlan(RunTier.CachedLaunch, RunDecisionReason.CacheValid, cache),
            BuildLevel.Csc => new RunPlan(RunTier.DirectCompile, RunDecisionReason.DirectCompilationRequired, cache),
            BuildLevel.All => new RunPlan(RunTier.MSBuildBuild, RunDecisionReason.FullBuildRequired, cache),
            _ => throw new ArgumentOutOfRangeException(nameof(buildLevel)),
        };
    }

    /// <summary>
    /// Determines whether a no-build invocation can launch complete synthetic CSC output without full cache validation.
    /// </summary>
    /// <param name="entryPointFileFullPath">The fully qualified entry-point path.</param>
    /// <param name="artifactsPath">The application artifacts directory.</param>
    /// <param name="probeDirectives">Probes changed source content for possible directives.</param>
    /// <param name="report">Receives planning diagnostics.</param>
    /// <returns>A launch-only plan when eligible; otherwise, a managed-fallback plan.</returns>
    internal static RunPlan AnalyzeNoBuildSynthetic(
        string entryPointFileFullPath,
        string artifactsPath,
        Func<FileBasedAppDirectiveProbeResult> probeDirectives,
        Action<string> report)
    {
        var successCacheFile = new FileInfo(Path.Join(artifactsPath, BuildSuccessCacheFileName));
        if (!successCacheFile.Exists)
        {
            report("Deferring to the managed CLI because the build success cache does not exist.");
            return new RunPlan(RunTier.ManagedFallback, RunDecisionReason.NoBuildNotEligible, Cache: null);
        }

        RunFileBuildCacheEntry? previousEntry = ReadCacheEntry(successCacheFile.FullName, report);
        if (previousEntry is not
            {
                BuildLevel: BuildLevel.Csc,
                Run: null,
                BuildResultFile: null,
            } ||
            !previousEntry.CscArguments.IsDefaultOrEmpty)
        {
            report("Deferring to the managed CLI because the previous build was not synthetic CSC.");
            return new RunPlan(RunTier.ManagedFallback, RunDecisionReason.NoBuildNotEligible, Cache: null);
        }

        var launchArtifacts = GetCscBuiltProgramLaunchArtifacts(entryPointFileFullPath, artifactsPath);
        if (GetMissingCscBuiltProgramLaunchArtifact(entryPointFileFullPath, artifactsPath) is { } missingArtifact)
        {
            report("Deferring to the managed CLI because a CSC launch artifact is missing: " + missingArtifact);
            return new RunPlan(RunTier.ManagedFallback, RunDecisionReason.NoBuildNotEligible, Cache: null);
        }

        var entryPointFile = new FileInfo(entryPointFileFullPath);
        if (!entryPointFile.Exists)
        {
            report("Deferring to the managed CLI because the entry point file is missing.");
            return new RunPlan(RunTier.ManagedFallback, RunDecisionReason.NoBuildNotEligible, Cache: null);
        }

        if (entryPointFile.LastWriteTimeUtc > successCacheFile.LastWriteTimeUtc &&
            probeDirectives() != FileBasedAppDirectiveProbeResult.None)
        {
            report("Deferring to the managed CLI because the changed source may contain file directives.");
            return new RunPlan(RunTier.ManagedFallback, RunDecisionReason.DirectiveProbeUnknown, Cache: null);
        }

        return new RunPlan(
            RunTier.LaunchOnly,
            RunDecisionReason.NoBuildSyntheticCache,
            Cache: null,
            new FileBasedAppLaunchInfo(launchArtifacts.AppHost, artifactsPath));
    }

    /// <summary>
    /// Validates an authoritative cache entry and produces its launch contract when still current.
    /// </summary>
    /// <param name="entryPointFileFullPath">The fully qualified entry-point path.</param>
    /// <param name="artifactsPath">The application artifacts directory.</param>
    /// <param name="globalProperties">The effective global properties.</param>
    /// <param name="sdkVersion">The current SDK version.</param>
    /// <param name="runtimeVersion">The current runtime version.</param>
    /// <param name="report">Receives planning diagnostics.</param>
    /// <returns>A cached-launch plan when valid; otherwise, a managed-fallback plan.</returns>
    internal static RunPlan AnalyzeCachedLaunch(
        string entryPointFileFullPath,
        string artifactsPath,
        Dictionary<string, string> globalProperties,
        string sdkVersion,
        string runtimeVersion,
        Action<string> report)
    {
        string successCachePath = Path.Join(artifactsPath, BuildSuccessCacheFileName);
        RunFileBuildCacheEntry? previousEntry = ReadCacheEntry(successCachePath, report);
        if (previousEntry is null)
        {
            return new RunPlan(RunTier.ManagedFallback, RunDecisionReason.CachedLaunchNotEligible, Cache: null);
        }

        var inputs = new FileBasedAppRunPlanInputs(
            EntryPointFileFullPath: entryPointFileFullPath,
            ArtifactsPath: artifactsPath,
            GlobalProperties: globalProperties,
            DirectiveInfo: new FileBasedAppDirectiveInfo(
                previousEntry.Directives.IsDefaultOrEmpty
                    ? FileBasedAppDirectiveProbeResult.None
                    : FileBasedAppDirectiveProbeResult.Present,
                CanCache: true,
                Directives: previousEntry.Directives),
            SdkVersion: sdkVersion,
            RuntimeVersion: runtimeVersion,
            NoCache: false,
            GetCscInputPaths: static () => []);
        RunPlan analyzedPlan = Analyze(inputs, report);
        if (analyzedPlan is not { Tier: RunTier.CachedLaunch, Cache.PreviousEntry: { } validatedEntry } ||
            !validatedEntry.Directives.SequenceEqual(previousEntry.Directives))
        {
            return new RunPlan(
                RunTier.ManagedFallback,
                RunDecisionReason.CachedLaunchNotEligible,
                analyzedPlan.Cache);
        }

        if (validatedEntry.Run is { Command.Length: > 0 } runProperties)
        {
            return analyzedPlan with
            {
                Launch = new FileBasedAppLaunchInfo(runProperties.Command, artifactsPath, runProperties),
            };
        }

        if (validatedEntry is
            {
                BuildLevel: BuildLevel.Csc,
                Run: null,
                BuildResultFile: null,
            } &&
            validatedEntry.CscArguments.IsDefaultOrEmpty)
        {
            var launchArtifacts = GetCscBuiltProgramLaunchArtifacts(entryPointFileFullPath, artifactsPath);
            return analyzedPlan with
            {
                Launch = new FileBasedAppLaunchInfo(launchArtifacts.AppHost, artifactsPath),
            };
        }

        return new RunPlan(
            RunTier.ManagedFallback,
            RunDecisionReason.CachedLaunchNotEligible,
            analyzedPlan.Cache);
    }

    private static BuildLevel AnalyzeBuildLevel(
        FileBasedAppRunPlanInputs inputs,
        Action<string> report,
        out FileBasedAppCacheInfo? cache)
    {
        if (inputs.NoCache)
        {
            report("Building because --no-cache was specified.");
            cache = ComputeCacheEntry(inputs, report);
            return BuildLevel.All;
        }

        if (!NeedsToBuild(inputs, report, out cache))
        {
            report("No need to build, the output is up to date. Cache: " + inputs.ArtifactsPath);
            return BuildLevel.None;
        }

        if (cache is null)
        {
            return BuildLevel.All;
        }

        if (cache.CanUseCscViaPreviousArguments)
        {
            report("We have CSC arguments from previous run. Skipping MSBuild and using CSC only.");
            Debug.Assert(cache.PreviousEntry != null);
            cache.CurrentEntry.CscArguments = cache.PreviousEntry.CscArguments;
            cache.CurrentEntry.BuildResultFile = cache.PreviousEntry.BuildResultFile;
            cache.CurrentEntry.Run = cache.PreviousEntry.Run;
            return BuildLevel.Csc;
        }

        RunFileBuildCacheEntry cacheEntry = cache.CurrentEntry;
        if (!cacheEntry.Directives.IsDefaultOrEmpty)
        {
            report("Using MSBuild because there are directives in the source file.");
            return BuildLevel.All;
        }

        var globalProperties = cacheEntry.GlobalProperties.Keys.Except(s_ignorableProperties, cacheEntry.GlobalProperties.Comparer);
        if (globalProperties.FirstOrDefault() is { } exampleKey)
        {
            string exampleValue = cacheEntry.GlobalProperties[exampleKey];
            report($"Using MSBuild because there are global properties, for example '{exampleKey}={exampleValue}'.");
            return BuildLevel.All;
        }

        if (cache.ExampleMSBuildFile is { } exampleMSBuildFile)
        {
            Debug.Assert(cacheEntry.ImplicitBuildFiles.Count != 0);
            report($"Using MSBuild because there are implicit build files, for example '{exampleMSBuildFile}'.");
            return BuildLevel.All;
        }

        foreach (string filePath in inputs.GetCscInputPaths())
        {
            if (!File.Exists(filePath))
            {
                report($"Using MSBuild because NuGet package file does not exist: {filePath}");
                return BuildLevel.All;
            }
        }

        report("Skipping MSBuild and using CSC only.");
        if (cache.PreviousEntry != null)
        {
            if (!cache.PreviousEntry.CscArguments.IsDefaultOrEmpty)
            {
                cache.InitialCanReuseAuxiliaryFiles = false;
            }

            cache.PreviousEntry.CscArguments = [];
            cache.PreviousEntry.BuildResultFile = null;
            cache.PreviousEntry.Run = null;
        }

        return BuildLevel.Csc;
    }

    /// <summary>
    /// Reads a successful-build cache entry.
    /// </summary>
    /// <param name="path">The cache file path.</param>
    /// <param name="report">Receives deserialization diagnostics.</param>
    /// <returns>The deserialized entry, or <see langword="null"/> when it cannot be read.</returns>
    internal static RunFileBuildCacheEntry? ReadCacheEntry(string path, Action<string> report)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize(stream, RunFileBuildCacheJsonSerializerContext.Default.RunFileBuildCacheEntry);
        }
        catch (Exception exception)
        {
            report($"Failed to deserialize cache entry ({path}): {exception.GetType().FullName}: {exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// Collects implicit files that can affect a file-based application build while walking ancestor directories.
    /// </summary>
    /// <param name="startDirectory">The entry-point directory.</param>
    /// <param name="collectedPaths">Receives full paths of discovered implicit files.</param>
    /// <param name="exampleMSBuildFile">Receives one discovered file whose presence requires MSBuild.</param>
    internal static void CollectImplicitBuildFiles(
        DirectoryInfo startDirectory,
        HashSet<string> collectedPaths,
        out string? exampleMSBuildFile)
    {
        exampleMSBuildFile = null;
        for (DirectoryInfo? directory = startDirectory; directory != null; directory = directory.Parent)
        {
            foreach (var implicitBuildFile in s_implicitBuildFiles)
            {
                string implicitBuildFilePath = Path.Join(directory.FullName, implicitBuildFile.Name);
                if (File.Exists(implicitBuildFilePath))
                {
                    collectedPaths.Add(implicitBuildFilePath);
                    if (implicitBuildFile.IsMSBuildFile && exampleMSBuildFile is null)
                    {
                        exampleMSBuildFile = implicitBuildFilePath;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the synthetic CSC launch artifacts for a file-based application.
    /// </summary>
    /// <param name="entryPointFileFullPath">The fully qualified entry-point path.</param>
    /// <param name="artifactsPath">The application artifacts directory.</param>
    /// <returns>The apphost, assembly, and runtime-configuration paths.</returns>
    internal static (string AppHost, string Assembly, string RuntimeConfig) GetCscBuiltProgramLaunchArtifacts(
        string entryPointFileFullPath,
        string artifactsPath)
    {
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(entryPointFileFullPath);
        string binDirectory = Path.Join(artifactsPath, "bin", "debug");
        return (
            Path.Join(binDirectory, fileNameWithoutExtension + FileNameSuffixes.CurrentPlatform.Exe),
            Path.Join(binDirectory, fileNameWithoutExtension + ".dll"),
            Path.Join(binDirectory, fileNameWithoutExtension + FileNameSuffixes.RuntimeConfigJson));
    }

    /// <summary>
    /// Finds the first missing synthetic CSC launch artifact.
    /// </summary>
    /// <param name="entryPointFileFullPath">The fully qualified entry-point path.</param>
    /// <param name="artifactsPath">The application artifacts directory.</param>
    /// <returns>The missing artifact path, or <see langword="null"/> when all required artifacts exist.</returns>
    internal static string? GetMissingCscBuiltProgramLaunchArtifact(
        string entryPointFileFullPath,
        string artifactsPath)
    {
        var launchArtifacts = GetCscBuiltProgramLaunchArtifacts(entryPointFileFullPath, artifactsPath);
        return new[]
        {
            launchArtifacts.AppHost,
            launchArtifacts.Assembly,
            launchArtifacts.RuntimeConfig,
        }.FirstOrDefault(static path => !File.Exists(path));
    }

    private static FileBasedAppCacheInfo? ComputeCacheEntry(FileBasedAppRunPlanInputs inputs, Action<string> report)
    {
        if (!inputs.DirectiveInfo.CanCache)
        {
            report("Skipping computing cache because there are project or ref directives.");
            return null;
        }

        var cacheEntry = new RunFileBuildCacheEntry(inputs.GlobalProperties)
        {
            Directives = inputs.DirectiveInfo.Directives,
            SdkVersion = inputs.SdkVersion,
            RuntimeVersion = inputs.RuntimeVersion,
        };
        var entryPointFile = new FileInfo(inputs.EntryPointFileFullPath);
        DirectoryInfo? entryPointFileDirectory = entryPointFile.Directory;
        Debug.Assert(entryPointFileDirectory != null);
        CollectImplicitBuildFiles(entryPointFileDirectory, cacheEntry.ImplicitBuildFiles, out string? exampleMSBuildFile);

        return new FileBasedAppCacheInfo
        {
            EntryPointFile = entryPointFile,
            CurrentEntry = cacheEntry,
            ExampleMSBuildFile = exampleMSBuildFile,
        };
    }

    private static bool NeedsToBuild(
        FileBasedAppRunPlanInputs inputs,
        Action<string> report,
        [NotNullWhen(returnValue: false)] out FileBasedAppCacheInfo? cache)
    {
        cache = ComputeCacheEntry(inputs, report);
        if (cache is null)
        {
            return true;
        }

        var successCacheFile = new FileInfo(Path.Join(inputs.ArtifactsPath, BuildSuccessCacheFileName));
        if (!successCacheFile.Exists)
        {
            report("Building because cache file does not exist: " + successCacheFile.FullName);
            return true;
        }

        var startCacheFile = new FileInfo(Path.Join(inputs.ArtifactsPath, BuildStartCacheFileName));
        if (!startCacheFile.Exists)
        {
            report("Building because start cache file does not exist: " + startCacheFile.FullName);
            return true;
        }

        DateTime buildTimeUtc = successCacheFile.LastWriteTimeUtc;
        if (startCacheFile.LastWriteTimeUtc > buildTimeUtc)
        {
            report("Building because start cache file is newer than success cache file (previous build likely failed): " + startCacheFile.FullName);
            return true;
        }

        Debug.Assert(!cache.TriedDeserializingPreviousEntry);
        RunFileBuildCacheEntry? previousCacheEntry = ReadCacheEntry(successCacheFile.FullName, report);
        cache.TriedDeserializingPreviousEntry = true;
        if (previousCacheEntry is null)
        {
            cache.InitialCanReuseAuxiliaryFiles = false;
            report("Building because previous cache entry could not be deserialized: " + successCacheFile.FullName);
            return true;
        }

        cache.PreviousEntry = previousCacheEntry;
        RunFileBuildCacheEntry cacheEntry = cache.CurrentEntry;
        if (previousCacheEntry.Run is { Command: { } previousRunCommand } &&
            Path.IsPathFullyQualified(previousRunCommand) &&
            !File.Exists(previousRunCommand))
        {
            report("Building because the run output is missing: " + previousRunCommand);
            return true;
        }

        if (previousCacheEntry.SdkVersion != cacheEntry.SdkVersion)
        {
            cache.InitialCanReuseAuxiliaryFiles = false;
            report($"Building because previous SDK version ({previousCacheEntry.SdkVersion}) does not match current ({cacheEntry.SdkVersion}): {successCacheFile.FullName}");
            return true;
        }

        if (previousCacheEntry.RuntimeVersion != cacheEntry.RuntimeVersion)
        {
            cache.InitialCanReuseAuxiliaryFiles = false;
            report($"Building because previous runtime version ({previousCacheEntry.RuntimeVersion}) does not match current ({cacheEntry.RuntimeVersion}): {successCacheFile.FullName}");
            return true;
        }

        if (previousCacheEntry.BuildResultFile is { Length: > 0 } buildResultFile &&
            !File.Exists(buildResultFile))
        {
            report("Building because the build result is missing: " + buildResultFile);
            return true;
        }

        if (previousCacheEntry is { BuildLevel: BuildLevel.Csc, Run: null } &&
            GetMissingCscBuiltProgramLaunchArtifact(inputs.EntryPointFileFullPath, inputs.ArtifactsPath) is { } missingArtifact)
        {
            report("Building because a CSC launch artifact is missing: " + missingArtifact);
            return true;
        }

        if (previousCacheEntry.GlobalProperties.Count != cacheEntry.GlobalProperties.Count)
        {
            report($"Building because previous global properties count ({previousCacheEntry.GlobalProperties.Count}) does not match current count ({cacheEntry.GlobalProperties.Count}): {successCacheFile.FullName}");
            return true;
        }

        foreach ((string key, string value) in cacheEntry.GlobalProperties)
        {
            if (!previousCacheEntry.GlobalProperties.TryGetValue(key, out string? otherValue) || value != otherValue)
            {
                report($"Building because previous global property \"{key}\" ({otherValue}) does not match current ({value}): {successCacheFile.FullName}");
                return true;
            }
        }

        FileInfo entryPointFile = cache.EntryPointFile;
        if (!entryPointFile.Exists)
        {
            report("Building because entry point file is missing: " + entryPointFile.FullName);
            return true;
        }

        string? reasonToNotReuseCscArguments = GetReasonToNotReuseCscArguments(cache);
        FileSystemInfo targetFile = ResolveLinkTargetOrSelf(entryPointFile);
        if (reasonToNotReuseCscArguments != null && targetFile.LastWriteTimeUtc > buildTimeUtc)
        {
            report("Compiling because entry point file is modified: " + targetFile.FullName);
            report(reasonToNotReuseCscArguments);
            return true;
        }

        foreach (string implicitBuildFilePath in previousCacheEntry.ImplicitBuildFiles)
        {
            FileSystemInfo implicitBuildFileInfo = ResolveLinkTargetOrSelf(new FileInfo(implicitBuildFilePath));
            if (!implicitBuildFileInfo.Exists || implicitBuildFileInfo.LastWriteTimeUtc > buildTimeUtc)
            {
                report("Building because implicit build file is missing or modified: " + implicitBuildFileInfo.FullName);
                return true;
            }
        }

        foreach (string implicitBuildFilePath in cacheEntry.ImplicitBuildFiles)
        {
            if (!previousCacheEntry.ImplicitBuildFiles.Contains(implicitBuildFilePath))
            {
                report("Building because new implicit build file is present: " + implicitBuildFilePath);
                return true;
            }
        }

        // Replayed CSC arguments are not supported when additional sources participate in the build.
        foreach (string additionalSourcePath in previousCacheEntry.AdditionalSources)
        {
            FileSystemInfo additionalSourceFileInfo = ResolveLinkTargetOrSelf(new FileInfo(additionalSourcePath));
            if (!additionalSourceFileInfo.Exists || additionalSourceFileInfo.LastWriteTimeUtc > buildTimeUtc)
            {
                report("Building because additional source file is missing or modified: " + additionalSourceFileInfo.FullName);
                return true;
            }
        }

        // This must remain the last stale-input check before enabling replayed CSC arguments.
        if (reasonToNotReuseCscArguments == null && targetFile.LastWriteTimeUtc > buildTimeUtc)
        {
            cache.CanUseCscViaPreviousArguments = true;
            report("Compiling because entry point file is modified: " + targetFile.FullName);
            return true;
        }

        return false;
    }

    private static FileSystemInfo ResolveLinkTargetOrSelf(FileSystemInfo fileSystemInfo)
    {
        if (!fileSystemInfo.Exists)
        {
            return fileSystemInfo;
        }

        return fileSystemInfo.ResolveLinkTarget(returnFinalTarget: true) ?? fileSystemInfo;
    }

    private static string? GetReasonToNotReuseCscArguments(FileBasedAppCacheInfo cache)
    {
        if (cache.PreviousEntry?.CscArguments.IsDefaultOrEmpty != false)
        {
            return "No CSC arguments from previous run.";
        }
        else if (cache.PreviousEntry.Run == null)
        {
            return "We have CSC arguments but not run properties. That's unexpected.";
        }
        else if (cache.PreviousEntry.BuildResultFile == null)
        {
            return "We have CSC arguments but not build result file. That's unexpected.";
        }
        else if (!cache.PreviousEntry.Directives.SequenceEqual(cache.CurrentEntry.Directives))
        {
            return "Cannot use CSC arguments from previous run because directives changed.";
        }

        return null;
    }
}
