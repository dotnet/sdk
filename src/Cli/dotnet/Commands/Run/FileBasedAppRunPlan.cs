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

    /// <summary>
    /// <c>IsMSBuildFile</c> is <see langword="true"/> if the presence of the implicit build file
    /// implies that CSC is not enough and MSBuild is needed to build the project, i.e., the file
    /// alone can affect MSBuild props or targets.
    /// </summary>
    /// <remarks>
    /// For example, the simple programs our CSC optimized path handles do not need NuGet restore,
    /// hence we can ignore NuGet config files.
    /// </remarks>
    private static readonly ImmutableArray<(string Name, bool IsMSBuildFile)> s_implicitBuildFiles =
    [
        ("global.json", false),

        // All these casings are recognized on case-sensitive platforms:
        // https://github.com/NuGet/NuGet.Client/blob/ab6b96fd9ba07ed3bf629ee389799ca4fb9a20fb/src/NuGet.Core/NuGet.Configuration/Settings/Settings.cs#L32-L37
        ("nuget.config", false),
        ("NuGet.config", false),
        ("NuGet.Config", false),

        ("Directory.Build.props", true),
        ("Directory.Build.targets", true),
        ("Directory.Packages.props", true),
        ("Directory.Build.rsp", true),
        ("MSBuild.rsp", true),
    ];

    /// <summary>
    /// For purposes of determining whether CSC is enough to build as opposed to full MSBuild,
    /// we can ignore properties that do not affect the build on their own.
    /// See also the <c>IsMSBuildFile</c> flag in <see cref="s_implicitBuildFiles"/>.
    /// </summary>
    /// <remarks>
    /// This is an <see cref="IEnumerable{T}"/> rather than <see cref="ImmutableArray{T}"/> to avoid boxing at the use site.
    /// </remarks>
    private static readonly IEnumerable<string> s_ignorableProperties =
    [
        // These are set by default by `dotnet run`, so at least these must be ignored otherwise the CSC optimization would not kick in by default.
        "NuGetInteractive",
        "_BuildNonexistentProjectsByDefault",
        "RestoreUseSkipNonexistentTargets",
        "ProvideCommandLineArgs",
    ];

    /// <summary>
    /// Computes the build level required by the current file-based application inputs.
    /// </summary>
    /// <param name="inputs">The current planning inputs.</param>
    /// <returns>The selected run plan.</returns>
    internal static RunPlan Analyze(FileBasedAppRunPlanInputs inputs)
    {
        BuildLevel buildLevel = AnalyzeBuildLevel(inputs, out FileBasedAppCacheInfo? cache);
        return buildLevel switch
        {
            BuildLevel.None => new RunPlan(RunTier.CachedLaunch, RunDecisionReason.CacheValid, cache),
            BuildLevel.Csc => new RunPlan(RunTier.DirectCompile, RunDecisionReason.DirectCompilationRequired, cache),
            BuildLevel.All => new RunPlan(RunTier.MSBuildBuild, RunDecisionReason.FullBuildRequired, cache),
            _ => throw new ArgumentOutOfRangeException(nameof(buildLevel)),
        };
    }

    /// <summary>
    /// Determines whether the Native AOT no-build path can reuse a synthetic CSC cache without full cache validation.
    /// </summary>
    /// <param name="entryPointFileFullPath">The fully qualified entry-point path.</param>
    /// <param name="artifactsPath">The application artifacts directory.</param>
    /// <returns>A launch-only plan when eligible; otherwise, a managed-fallback plan.</returns>
    internal static RunPlan AnalyzeAotNoBuildSynthetic(
        string entryPointFileFullPath,
        string artifactsPath)
    {
        var successCacheFile = new FileInfo(Path.Join(artifactsPath, BuildSuccessCacheFileName));
        if (!successCacheFile.Exists)
        {
            Reporter.Verbose.WriteLine("Falling back to the managed CLI because the build success cache does not exist.");
            return new RunPlan(RunTier.ManagedFallback, RunDecisionReason.NoBuildNotEligible, Cache: null);
        }

        RunFileBuildCacheEntry? previousEntry = ReadCacheEntry(successCacheFile.FullName);
        if (previousEntry is not
            {
                BuildLevel: BuildLevel.Csc,
                Run: null,
                BuildResultFile: null,
            } ||
            !previousEntry.CscArguments.IsDefaultOrEmpty)
        {
            Reporter.Verbose.WriteLine("Falling back to the managed CLI because the previous build was not synthetic CSC.");
            return new RunPlan(RunTier.ManagedFallback, RunDecisionReason.NoBuildNotEligible, Cache: null);
        }

        if (!File.Exists(entryPointFileFullPath))
        {
            Reporter.Verbose.WriteLine("Falling back to the managed CLI because the entry point file is missing.");
            return new RunPlan(RunTier.ManagedFallback, RunDecisionReason.NoBuildNotEligible, Cache: null);
        }

        return new RunPlan(
            RunTier.LaunchOnly,
            RunDecisionReason.NoBuildSyntheticCache,
            Cache: null,
            new FileBasedAppLaunchInfo(
                GetCscBuiltProgramLaunchArtifacts(entryPointFileFullPath, artifactsPath).AppHost,
                artifactsPath));
    }

    /// <summary>
    /// Validates an authoritative cache entry and produces its launch contract when still current.
    /// </summary>
    /// <param name="entryPointFileFullPath">The fully qualified entry-point path.</param>
    /// <param name="artifactsPath">The application artifacts directory.</param>
    /// <param name="globalProperties">The effective global properties.</param>
    /// <param name="sdkVersion">The current SDK version.</param>
    /// <param name="runtimeVersion">The current runtime version.</param>
    /// <returns>A cached-launch plan when valid; otherwise, a managed-fallback plan.</returns>
    internal static RunPlan AnalyzeCachedLaunch(
        string entryPointFileFullPath,
        string artifactsPath,
        Dictionary<string, string> globalProperties,
        string sdkVersion,
        string runtimeVersion)
    {
        string successCachePath = Path.Join(artifactsPath, BuildSuccessCacheFileName);
        RunFileBuildCacheEntry? previousEntry = ReadCacheEntry(successCachePath);
        if (previousEntry is null)
        {
            return new RunPlan(RunTier.ManagedFallback, RunDecisionReason.CachedLaunchNotEligible, Cache: null);
        }

        var inputs = new FileBasedAppRunPlanInputs(
            EntryPointFileFullPath: entryPointFileFullPath,
            ArtifactsPath: artifactsPath,
            GlobalProperties: globalProperties,
            CanCache: true,
            Directives: previousEntry.Directives,
            SdkVersion: sdkVersion,
            RuntimeVersion: runtimeVersion,
            NoCache: false,
            GetCscInputPaths: static () => []);
        RunPlan analyzedPlan = Analyze(inputs);
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
        out FileBasedAppCacheInfo? cache)
    {
        if (inputs.NoCache)
        {
            Reporter.Verbose.WriteLine("Building because --no-cache was specified.");
            cache = ComputeCacheEntry(inputs);
            return BuildLevel.All;
        }

        if (!NeedsToBuild(inputs, out cache))
        {
            Reporter.Verbose.WriteLine("No need to build, the output is up to date. Cache: " + inputs.ArtifactsPath);
            return BuildLevel.None;
        }

        if (cache is null)
        {
            return BuildLevel.All;
        }

        if (cache.CanUseCscViaPreviousArguments)
        {
            Reporter.Verbose.WriteLine("We have CSC arguments from previous run. Skipping MSBuild and using CSC only.");

            // Keep the cached info for next time, so we can use CSC again.
            Debug.Assert(cache.PreviousEntry != null);
            cache.CurrentEntry.CscArguments = cache.PreviousEntry.CscArguments;
            cache.CurrentEntry.BuildResultFile = cache.PreviousEntry.BuildResultFile;
            cache.CurrentEntry.Run = cache.PreviousEntry.Run;
            return BuildLevel.Csc;
        }

        // Determine whether we can use CSC only or need to use MSBuild.
        RunFileBuildCacheEntry cacheEntry = cache.CurrentEntry;
        if (!cacheEntry.Directives.IsDefaultOrEmpty)
        {
            Reporter.Verbose.WriteLine("Using MSBuild because there are directives in the source file.");
            return BuildLevel.All;
        }

        var globalProperties = cacheEntry.GlobalProperties.Keys.Except(s_ignorableProperties, cacheEntry.GlobalProperties.Comparer);
        if (globalProperties.FirstOrDefault() is { } exampleKey)
        {
            string exampleValue = cacheEntry.GlobalProperties[exampleKey];
            Reporter.Verbose.WriteLine($"Using MSBuild because there are global properties, for example '{exampleKey}={exampleValue}'.");
            return BuildLevel.All;
        }

        if (cache.ExampleMSBuildFile is { } exampleMSBuildFile)
        {
            Debug.Assert(cacheEntry.ImplicitBuildFiles.Count != 0);
            Reporter.Verbose.WriteLine($"Using MSBuild because there are implicit build files, for example '{exampleMSBuildFile}'.");
            return BuildLevel.All;
        }

        foreach (string filePath in inputs.GetCscInputPaths())
        {
            if (!File.Exists(filePath))
            {
                Reporter.Verbose.WriteLine($"Using MSBuild because NuGet package file does not exist: {filePath}");
                return BuildLevel.All;
            }
        }

        Reporter.Verbose.WriteLine("Skipping MSBuild and using CSC only.");

        // Don't reuse CSC arguments, this is the simple CSC-only build where we use hard-coded CSC arguments.
        if (cache.PreviousEntry != null)
        {
            // If we reused CSC arguments in the previous run and want to use hard-coded CSC arguments
            // in this run, we cannot reuse the csc.rsp file.
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
    /// <returns>The deserialized entry, or <see langword="null"/> when it cannot be read.</returns>
    internal static RunFileBuildCacheEntry? ReadCacheEntry(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize(stream, RunFileBuildCacheJsonSerializerContext.Default.RunFileBuildCacheEntry);
        }
        catch (Exception exception)
        {
            Reporter.Verbose.WriteLine($"Failed to deserialize cache entry ({path}): {exception.GetType().FullName}: {exception.Message}");
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
                FileSystemInfo implicitBuildFileInfo = new FileInfo(Path.Join(directory.FullName, implicitBuildFile.Name));
                implicitBuildFileInfo = ResolveLinkTargetOrSelf(implicitBuildFileInfo);
                if (implicitBuildFileInfo.Exists)
                {
                    collectedPaths.Add(implicitBuildFileInfo.FullName);
                    if (implicitBuildFile.IsMSBuildFile && exampleMSBuildFile is null)
                    {
                        exampleMSBuildFile = implicitBuildFileInfo.FullName;
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
    /// Touching the artifacts folder ensures it is considered recently used and not removed by
    /// <see cref="Clean.FileBasedAppArtifacts.CleanFileBasedAppArtifactsCommand"/>.
    /// </summary>
    /// <param name="artifactsPath">The application artifacts directory.</param>
    internal static void MarkArtifactsPathUsed(string artifactsPath)
    {
        try
        {
            Directory.SetLastWriteTimeUtc(artifactsPath, DateTime.UtcNow);
        }
        catch (Exception exception)
        {
            Reporter.Verbose.WriteLine($"Cannot touch folder '{artifactsPath}': {exception}");
        }
    }

    /// <summary>
    /// Compute current cache entry - we need to do this always (except if we already know we will skip saving the cache):
    /// <list type="bullet">
    /// <item>if we can skip build, we still need to check everything in the cache entry (e.g., implicit build files)</item>
    /// <item>if we have to build, we need to have the cache entry to write it to the success cache file</item>
    /// </list>
    /// </summary>
    private static FileBasedAppCacheInfo? ComputeCacheEntry(FileBasedAppRunPlanInputs inputs)
    {
        if (!inputs.CanCache)
        {
            Reporter.Verbose.WriteLine("Skipping computing cache because there are project or ref directives.");
            return null;
        }

        var originalEntryPointFile = new FileInfo(inputs.EntryPointFileFullPath);
        var entryPointFile = (FileInfo)ResolveLinkTargetOrSelf(originalEntryPointFile);
        var cacheEntry = new RunFileBuildCacheEntry(inputs.GlobalProperties)
        {
            AliasedEntryPointFilePath = entryPointFile != originalEntryPointFile ? entryPointFile.FullName : null,
            Directives = inputs.Directives,
            SdkVersion = inputs.SdkVersion,
            RuntimeVersion = inputs.RuntimeVersion,
        };

        DirectoryInfo? originalEntryPointFileDirectory = originalEntryPointFile.Directory;
        Debug.Assert(originalEntryPointFileDirectory != null);
        CollectImplicitBuildFiles(originalEntryPointFileDirectory, cacheEntry.ImplicitBuildFiles, out string? exampleMSBuildFile);

        return new FileBasedAppCacheInfo
        {
            EntryPointFile = entryPointFile,
            CurrentEntry = cacheEntry,
            ExampleMSBuildFile = exampleMSBuildFile,
        };
    }

    private static bool NeedsToBuild(
        FileBasedAppRunPlanInputs inputs,
        [NotNullWhen(returnValue: false)] out FileBasedAppCacheInfo? cache)
    {
        cache = ComputeCacheEntry(inputs);
        if (cache is null)
        {
            return true;
        }

        // Check cache files.
        var successCacheFile = new FileInfo(Path.Join(inputs.ArtifactsPath, BuildSuccessCacheFileName));
        if (!successCacheFile.Exists)
        {
            Reporter.Verbose.WriteLine("Building because cache file does not exist: " + successCacheFile.FullName);
            return true;
        }

        var startCacheFile = new FileInfo(Path.Join(inputs.ArtifactsPath, BuildStartCacheFileName));
        if (!startCacheFile.Exists)
        {
            Reporter.Verbose.WriteLine("Building because start cache file does not exist: " + startCacheFile.FullName);
            return true;
        }

        DateTime buildTimeUtc = successCacheFile.LastWriteTimeUtc;
        if (startCacheFile.LastWriteTimeUtc > buildTimeUtc)
        {
            Reporter.Verbose.WriteLine("Building because start cache file is newer than success cache file (previous build likely failed): " + startCacheFile.FullName);
            return true;
        }

        Debug.Assert(!cache.TriedDeserializingPreviousEntry);
        RunFileBuildCacheEntry? previousCacheEntry = ReadCacheEntry(successCacheFile.FullName);
        cache.TriedDeserializingPreviousEntry = true;
        if (previousCacheEntry is null)
        {
            cache.InitialCanReuseAuxiliaryFiles = false;
            Reporter.Verbose.WriteLine("Building because previous cache entry could not be deserialized: " + successCacheFile.FullName);
            return true;
        }

        cache.PreviousEntry = previousCacheEntry;
        RunFileBuildCacheEntry cacheEntry = cache.CurrentEntry;
        if (previousCacheEntry.Run is { Command: { } previousRunCommand } &&
            Path.IsPathFullyQualified(previousRunCommand) &&
            !File.Exists(previousRunCommand))
        {
            Reporter.Verbose.WriteLine("Building because the run output is missing: " + previousRunCommand);
            return true;
        }

        // Check that versions match.
        if (previousCacheEntry.SdkVersion != cacheEntry.SdkVersion)
        {
            cache.InitialCanReuseAuxiliaryFiles = false;
            Reporter.Verbose.WriteLine($"Building because previous SDK version ({previousCacheEntry.SdkVersion}) does not match current ({cacheEntry.SdkVersion}): {successCacheFile.FullName}");
            return true;
        }

        if (previousCacheEntry.RuntimeVersion != cacheEntry.RuntimeVersion)
        {
            cache.InitialCanReuseAuxiliaryFiles = false;
            Reporter.Verbose.WriteLine($"Building because previous runtime version ({previousCacheEntry.RuntimeVersion}) does not match current ({cacheEntry.RuntimeVersion}): {successCacheFile.FullName}");
            return true;
        }

        // Check that properties match.
        if (previousCacheEntry.GlobalProperties.Count != cacheEntry.GlobalProperties.Count)
        {
            Reporter.Verbose.WriteLine($"Building because previous global properties count ({previousCacheEntry.GlobalProperties.Count}) does not match current count ({cacheEntry.GlobalProperties.Count}): {successCacheFile.FullName}");
            return true;
        }

        foreach ((string key, string value) in cacheEntry.GlobalProperties)
        {
            if (!previousCacheEntry.GlobalProperties.TryGetValue(key, out string? otherValue) || value != otherValue)
            {
                Reporter.Verbose.WriteLine($"Building because previous global property \"{key}\" ({otherValue}) does not match current ({value}): {successCacheFile.FullName}");
                return true;
            }
        }

        FileInfo entryPointFile = cache.EntryPointFile;
        // If the source file does not exist, we want to build so proper errors are reported.
        if (!entryPointFile.Exists)
        {
            Reporter.Verbose.WriteLine("Building because entry point file is missing: " + entryPointFile.FullName);
            return true;
        }

        // Check that aliased entry point file path matches.
        if (previousCacheEntry.AliasedEntryPointFilePath != cacheEntry.AliasedEntryPointFilePath)
        {
            Reporter.Verbose.WriteLine($"Building because aliased entry point file path changed from '{previousCacheEntry.AliasedEntryPointFilePath}' to '{cacheEntry.AliasedEntryPointFilePath}': {successCacheFile.FullName}");
            return true;
        }

        string? reasonToNotReuseCscArguments = GetReasonToNotReuseCscArguments(cache);
        // Check that the source file is not modified.
        // Only do this here if we cannot reuse CSC arguments (then checking this first is faster); otherwise we need to check implicit build files anyway.
        if (reasonToNotReuseCscArguments != null && entryPointFile.LastWriteTimeUtc > buildTimeUtc)
        {
            Reporter.Verbose.WriteLine("Compiling because entry point file is modified: " + entryPointFile.FullName);
            Reporter.Verbose.WriteLine(reasonToNotReuseCscArguments);
            return true;
        }

        // Check that implicit build files are not modified.
        foreach (string implicitBuildFilePath in previousCacheEntry.ImplicitBuildFiles)
        {
            FileSystemInfo implicitBuildFileInfo = new FileInfo(implicitBuildFilePath);
            if (!implicitBuildFileInfo.Exists || implicitBuildFileInfo.LastWriteTimeUtc > buildTimeUtc)
            {
                Reporter.Verbose.WriteLine("Building because implicit build file is missing or modified: " + implicitBuildFileInfo.FullName);
                return true;
            }
        }

        // Check that no new implicit build files are present.
        foreach (string implicitBuildFilePath in cacheEntry.ImplicitBuildFiles)
        {
            if (!previousCacheEntry.ImplicitBuildFiles.Contains(implicitBuildFilePath))
            {
                Reporter.Verbose.WriteLine("Building because new implicit build file is present: " + implicitBuildFilePath);
                return true;
            }
        }

        // Check that additional sources are not modified.
        // NOTE: We currently don't support the CSC-arg-reuse optimization through additional sources (i.e., we don't set `CanUseCscViaPreviousArguments=true` here).
        //       If that changes, we will also need to make sure `RunFileBuildCacheEntry.Directives` contains directives from other files
        //       (as that is used to determine whether we can reuse CSC args, see `GetReasonToNotReuseCscArguments`).
        foreach (string additionalSourcePath in previousCacheEntry.AdditionalSources)
        {
            FileSystemInfo additionalSourceFileInfo = new FileInfo(additionalSourcePath);
            if (!additionalSourceFileInfo.Exists || additionalSourceFileInfo.LastWriteTimeUtc > buildTimeUtc)
            {
                Reporter.Verbose.WriteLine("Building because additional source file is missing or modified: " + additionalSourceFileInfo.FullName);
                return true;
            }
        }

        // This must remain the last stale-input check before enabling replayed CSC arguments.
        if (reasonToNotReuseCscArguments == null && entryPointFile.LastWriteTimeUtc > buildTimeUtc)
        {
            cache.CanUseCscViaPreviousArguments = true;
            Reporter.Verbose.WriteLine("Compiling because entry point file is modified: " + entryPointFile.FullName);
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
