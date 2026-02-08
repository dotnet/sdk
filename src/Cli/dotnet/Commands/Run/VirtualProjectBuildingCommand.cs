// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.SimpleErrorLogger;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.Cli.Commands.Clean.FileBasedAppArtifacts;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Used to build a virtual project file in memory to support <c>dotnet run file.cs</c>.
/// </summary>
internal sealed class VirtualProjectBuildingCommand : CommandBase
{
    /// <summary>
    /// A file put into the artifacts directory when build starts.
    /// It contains full path to the original source file to allow tracking down the input corresponding to the output.
    /// It is also used to check whether the previous build has failed (when it is newer than the <see cref="BuildSuccessCacheFileName"/>).
    /// </summary>
    private const string BuildStartCacheFileName = "build-start.cache";

    /// <summary>
    /// A file written in the artifacts directory on successful builds used to determine whether a re-build is needed.
    /// </summary>
    private const string BuildSuccessCacheFileName = "build-success.cache";

    internal const string FileBasedProgramCanSkipMSBuild = nameof(FileBasedProgramCanSkipMSBuild);

    /// <summary>
    /// <c>IsMSBuildFile</c> is <see langword="true"/> if the presence of the implicit build file (even if there are no <see cref="CSharpDirective"/>s)
    /// implies that CSC is not enough and MSBuild is needed to build the project, i.e., the file alone can affect MSBuild props or targets.
    /// </summary>
    /// <remarks>
    /// For example, the simple programs our CSC optimized path handles do not need NuGet restore, hence we can ignore NuGet config files.
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

    public static string TargetFrameworkVersion => Product.TargetFrameworkVersion;

    public bool NoRestore { get; init; }

    /// <summary>
    /// If <see langword="true"/>, build markers are not checked and hence MSBuild is always run.
    /// This property does not control whether the build markers are written, use <see cref="NoWriteBuildMarkers"/> for that.
    /// </summary>
    public bool NoCache { get; init; }

    public bool NoBuild { get; init; }

    /// <summary>
    /// Filled during <see cref="Execute"/>.
    /// </summary>
    public (BuildLevel Level, CacheInfo? Cache) LastBuild { get; private set; }

    /// <summary>
    /// If <see langword="true"/>, no build markers are written
    /// (like <see cref="BuildStartCacheFileName"/> and <see cref="BuildSuccessCacheFileName"/>).
    /// Also skips automatic cleanup.
    /// This property does not control whether the markers are checked, use <see cref="NoCache"/> for that.
    /// </summary>
    public bool NoWriteBuildMarkers { get; init; }

    public VirtualProjectBuilder Builder { get; }
    public MSBuildArgs MSBuildArgs { get; }

    public ImmutableArray<CSharpDirective> Directives
    {
        get
        {
            if (field.IsDefault)
            {
                field = FileLevelDirectiveHelpers.FindDirectives(Builder.EntryPointSourceFile, reportAllErrors: false, ThrowingReporter);
                Debug.Assert(!field.IsDefault);
            }

            return field;
        }

        set;
    }

    public VirtualProjectBuildingCommand(
        string entryPointFileFullPath,
        MSBuildArgs msbuildArgs,
        string? artifactsPath = null)
    {
        MSBuildArgs = msbuildArgs.CloneWithAdditionalProperties(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // See https://github.com/dotnet/msbuild/blob/main/documentation/specs/build-nonexistent-projects-by-default.md.
            { "_BuildNonexistentProjectsByDefault", bool.TrueString },
            { "RestoreUseSkipNonexistentTargets", bool.FalseString },
            { "ProvideCommandLineArgs", bool.TrueString },
        }
        .AsReadOnly());

        Builder = new VirtualProjectBuilder(entryPointFileFullPath, TargetFrameworkVersion, MSBuildArgs.GetResolvedTargets(), artifactsPath);
    }

    public override int Execute()
    {
        bool msbuildGet = MSBuildArgs.GetProperty is [_, ..] || MSBuildArgs.GetItem is [_, ..] || MSBuildArgs.GetTargetResult is [_, ..];
        bool evalOnly = msbuildGet && Builder.RequestedTargets is null or [];
        bool minimizeStdOut = msbuildGet && MSBuildArgs.GetResultOutputFile is null or [];

        var verbosity = MSBuildArgs.Verbosity ?? MSBuildForwardingAppWithoutLogging.DefaultVerbosity;
        var consoleLogger = minimizeStdOut
            ? new SimpleErrorLogger()
            : CommonRunHelpers.GetConsoleLogger(MSBuildArgs.CloneWithExplicitArgs([$"--verbosity:{verbosity}", .. MSBuildArgs.OtherMSBuildArgs]));
        var binaryLogger = GetBinaryLogger(MSBuildArgs.OtherMSBuildArgs);

        CacheInfo? cache = null;

        if (msbuildGet)
        {
            LastBuild = (BuildLevel.None, Cache: null);
        }
        else if (NoBuild)
        {
            // This is reached only during `restore`, not `run --no-build`
            // (in the latter case, this virtual building command is not executed at all).
            Debug.Assert(!NoRestore);

            LastBuild = (BuildLevel.None, Cache: null);

            if (!NoWriteBuildMarkers)
            {
                CreateTempSubdirectory(Builder.ArtifactsPath);
                MarkArtifactsFolderUsed();
            }
        }
        else
        {
            if (NoCache)
            {
                cache = ComputeCacheEntry();
                cache.CurrentEntry.BuildLevel = BuildLevel.All;
                LastBuild = (BuildLevel.All, cache);
            }
            else
            {
                var buildLevel = GetBuildLevel(out cache);
                cache.CurrentEntry.BuildLevel = buildLevel;
                LastBuild = (buildLevel, cache);

                if (buildLevel is BuildLevel.None)
                {
                    if (binaryLogger is not null)
                    {
                        Reporter.Output.WriteLine(CliCommandStrings.NoBinaryLogBecauseUpToDate.Yellow());
                    }

                    // No rebuild, can reuse run properties.
                    cache.CurrentEntry.Run = cache.PreviousEntry?.Run;

                    MarkArtifactsFolderUsed();
                    return 0;
                }

                if (buildLevel is BuildLevel.Csc)
                {
                    MarkBuildStart();

                    // Execute CSC.
                    int result = new CSharpCompilerCommand
                    {
                        EntryPointFileFullPath = Builder.EntryPointFileFullPath,
                        ArtifactsPath = Builder.ArtifactsPath,
                        CanReuseAuxiliaryFiles = cache.DetermineFinalCanReuseAuxiliaryFiles(),
                        CscArguments = cache.PreviousEntry?.CscArguments ?? [],
                        BuildResultFile = cache.PreviousEntry?.BuildResultFile,
                    }
                    .Execute(out bool fallbackToNormalBuild);

                    if (!fallbackToNormalBuild)
                    {
                        if (result == 0)
                        {
                            MarkBuildSuccess(cache);
                        }

                        if (binaryLogger is not null)
                        {
                            Reporter.Output.WriteLine(CliCommandStrings.NoBinaryLogBecauseRunningJustCsc.Yellow());
                        }

                        return result;
                    }

                    Debug.Assert(result != 0);
                }

                Debug.Assert(buildLevel is BuildLevel.All or BuildLevel.Csc);
            }

            MarkBuildStart();
        }

        if (!NoWriteBuildMarkers && !msbuildGet)
        {
            CleanFileBasedAppArtifactsCommand.StartAutomaticCleanupIfNeeded();
        }

        Dictionary<string, string?> savedEnvironmentVariables = [];
        try
        {
            // Set environment variables.
            foreach (var (key, value) in MSBuildForwardingAppWithoutLogging.GetMSBuildRequiredEnvironmentVariables())
            {
                savedEnvironmentVariables[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }

            // Set up MSBuild.
            ReadOnlySpan<ILogger> binaryLoggers = binaryLogger is null ? [] : [binaryLogger.Value];
            IEnumerable<ILogger> loggers = [.. binaryLoggers, consoleLogger];
            var projectCollection = new ProjectCollection(
                MSBuildArgs.GlobalProperties,
                loggers,
                ToolsetDefinitionLocations.Default);
            var parameters = new BuildParameters(projectCollection)
            {
                Loggers = loggers,
                LogTaskInputs = binaryLoggers.Length != 0,
            };

            BuildManager.DefaultBuildManager.BeginBuild(parameters);

            int exitCode = 0;
            ProjectInstance? projectInstance = null;
            BuildResult? buildOrRestoreResult = null;

            // Do a restore first (equivalent to MSBuild's "implicit restore", i.e., `/restore`).
            // See https://github.com/dotnet/msbuild/blob/a1c2e7402ef0abe36bf493e395b04dd2cb1b3540/src/MSBuild/XMake.cs#L1838
            // and https://github.com/dotnet/msbuild/issues/11519.
            if (!NoRestore && !evalOnly)
            {
                var restoreRequest = new BuildRequestData(
                    CreateProjectInstance(projectCollection, addGlobalProperties: AddRestoreGlobalProperties(MSBuildArgs.RestoreGlobalProperties)),
                    targetsToBuild: ["Restore"],
                    hostServices: null,
                    BuildRequestDataFlags.ClearCachesAfterBuild | BuildRequestDataFlags.SkipNonexistentTargets | BuildRequestDataFlags.IgnoreMissingEmptyAndInvalidImports | BuildRequestDataFlags.FailOnUnresolvedSdk);

                var restoreResult = BuildManager.DefaultBuildManager.BuildRequest(restoreRequest);
                if (restoreResult.OverallResult != BuildResultCode.Success)
                {
                    exitCode = 1;
                }

                projectInstance = restoreRequest.ProjectInstance;
                buildOrRestoreResult = restoreResult;
            }

            // Then do a build.
            if (exitCode == 0 && !NoBuild && !evalOnly)
            {
                var buildRequest = new BuildRequestData(
                    CreateProjectInstance(projectCollection),
                    targetsToBuild: Builder.RequestedTargets ?? [Constants.Build, Constants.CoreCompile]);

                var buildResult = BuildManager.DefaultBuildManager.BuildRequest(buildRequest);
                if (buildResult.OverallResult != BuildResultCode.Success)
                {
                    exitCode = 1;
                }

                if (exitCode == 0 && !msbuildGet)
                {
                    Debug.Assert(cache != null);
                    Debug.Assert(buildRequest.ProjectInstance != null);

                    // Cache run info (to avoid re-evaluating the project instance).
                    cache.CurrentEntry.Run = RunProperties.TryFromProject(buildRequest.ProjectInstance, out var runProperties)
                        ? runProperties
                        : null;

                    if (!MSBuildUtilities.ConvertStringToBool(buildRequest.ProjectInstance.GetPropertyValue(FileBasedProgramCanSkipMSBuild), defaultValue: true))
                    {
                        Reporter.Verbose.WriteLine($"Not saving cache because there is an opt-out via MSBuild property {FileBasedProgramCanSkipMSBuild}.");
                    }
                    else
                    {
                        CacheCscArguments(cache, buildResult);

                        MarkBuildSuccess(cache);
                    }
                }

                projectInstance = buildRequest.ProjectInstance;
                buildOrRestoreResult = buildResult;
            }

            // Print build information.
            if (msbuildGet)
            {
                projectInstance ??= CreateProjectInstance(projectCollection);
                PrintBuildInformation(projectCollection, projectInstance, buildOrRestoreResult);
            }

            BuildManager.DefaultBuildManager.EndBuild();
            consoleLogger = null; // avoid double disposal which would throw

            return exitCode;
        }
        catch (Exception e)
        {
            Reporter.Error.WriteLine(CommandLoggingContext.IsVerbose ?
                e.ToString().Red().Bold() :
                e.Message.Red().Bold());
            return 1;
        }
        finally
        {
            foreach (var (key, value) in savedEnvironmentVariables)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            binaryLogger?.Value.ReallyShutdown();
            consoleLogger?.Shutdown();
        }

        static Action<IDictionary<string, string>> AddRestoreGlobalProperties(ReadOnlyDictionary<string, string>? restoreProperties)
        {
            return globalProperties =>
            {
                globalProperties["MSBuildRestoreSessionId"] = Guid.NewGuid().ToString("D");
                globalProperties["MSBuildIsRestoring"] = bool.TrueString;
                foreach (var (key, value) in RestoringCommand.RestoreOptimizationProperties)
                {
                    globalProperties[key] = value;
                }
                if (restoreProperties is null)
                {
                    return;
                }
                foreach (var (key, value) in restoreProperties)
                {
                    if (value is not null)
                    {
                        globalProperties[key] = value;
                    }
                }
            };
        }

        static Lazy<FacadeLogger>? GetBinaryLogger(IReadOnlyList<string>? args)
        {
            if (args is null) return null;
            // Like in MSBuild, only the last binary logger is used.
            for (int i = args.Count - 1; i >= 0; i--)
            {
                var arg = args[i];
                if (LoggerUtility.IsBinLogArgument(arg))
                {
                    // We don't want to create the binlog file until actually needed, hence we wrap this in a Lazy.
                    return new(() =>
                    {
                        var logger = new BinaryLogger
                        {
                            Parameters = arg.IndexOf(':') is >= 0 and var index
                                ? arg[(index + 1)..]
                                : "msbuild.binlog",
                        };
                        return LoggerUtility.CreateFacadeLogger([logger]);
                    });
                }
            }

            return null;
        }

        void CacheCscArguments(CacheInfo cache, BuildResult result)
        {
            // We cannot reuse CSC arguments from previous run and skip MSBuild if there are project references
            // because we cannot easily detect whether any referenced projects have changed.
            if (Directives.Any(static d => d is CSharpDirective.Project))
            {
                Reporter.Verbose.WriteLine("Not saving CSC arguments because there is a project directive.");
                return;
            }

            if (result.TryGetResultsForTarget(Constants.CoreCompile, out var coreCompileResult) &&
                coreCompileResult.ResultCode == TargetResultCode.Success &&
                result.TryGetResultsForTarget(Constants.Build, out var buildResult) &&
                buildResult.ResultCode == TargetResultCode.Success &&
                buildResult.Items is [{ } buildResultItem])
            {
                if (coreCompileResult.Items.Length == 0)
                {
                    EnsurePreviousCacheEntry(cache);
                    cache.CurrentEntry.CscArguments = cache.PreviousEntry?.CscArguments ?? [];
                    cache.CurrentEntry.BuildResultFile = cache.PreviousEntry?.BuildResultFile;
                    Reporter.Verbose.WriteLine($"Reusing previous CSC arguments ({cache.CurrentEntry.CscArguments.Length}) because none were found in the {Constants.CoreCompile} target.");
                }
                else
                {
                    cache.CurrentEntry.CscArguments = coreCompileResult.Items
                        .Select(static i => i.GetMetadata(Constants.Identity))
                        .Where(static a => a != "/noconfig") // this option cannot be in the rsp file
                        .Select(Escape)
                        .ToImmutableArray();
                    cache.CurrentEntry.BuildResultFile = buildResultItem.GetMetadata(Constants.FullPath);
                    Reporter.Verbose.WriteLine($"Found CSC arguments ({cache.CurrentEntry.CscArguments.Length}) and build result path: {cache.CurrentEntry.BuildResultFile}");
                }
            }
            else
            {
                Reporter.Verbose.WriteLine($"No CSC arguments found in targets: {string.Join(", ", result.ResultsByTarget.Keys)}");
            }

            // Arguments coming from CoreCompile are escaped if they are in the form of `/option:"some path"`
            // but not if they are standalone paths - we need to escape the latter kind ourselves.
            static string Escape(string arg)
            {
                if (!Patterns.EscapedCompilerOption.IsMatch(arg))
                {
                    return CSharpCompilerCommand.EscapePathArgument(arg);
                }

                return arg;
            }
        }

        void PrintBuildInformation(ProjectCollection projectCollection, ProjectInstance projectInstance, BuildResult? buildOrRestoreResult)
        {
            var resultOutputFile = MSBuildArgs.GetResultOutputFile is [{ } file, ..] ? file : null;

            // If a single property is requested, don't print as JSON.
            if (MSBuildArgs is { GetProperty: [{ } singlePropertyName], GetItem: null or [], GetTargetResult: null or [] })
            {
                var result = projectInstance.GetPropertyValue(singlePropertyName);
                if (resultOutputFile == null)
                {
                    Console.WriteLine(result);
                }
                else
                {
                    File.WriteAllText(path: resultOutputFile, contents: result + Environment.NewLine);
                }
            }
            else
            {
                using var stream = resultOutputFile == null
                   ? Console.OpenStandardOutput()
                   : new FileStream(resultOutputFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
                writer.WriteStartObject();

                if (MSBuildArgs.GetProperty is [_, ..])
                {
                    writer.WritePropertyName("Properties");
                    writer.WriteStartObject();

                    foreach (var propertyName in MSBuildArgs.GetProperty)
                    {
                        writer.WriteString(propertyName, projectInstance.GetPropertyValue(propertyName));
                    }

                    writer.WriteEndObject();
                }

                if (MSBuildArgs.GetItem is [_, ..])
                {
                    writer.WritePropertyName("Items");
                    writer.WriteStartObject();

                    foreach (var itemName in MSBuildArgs.GetItem)
                    {
                        writer.WritePropertyName(itemName);
                        writer.WriteStartArray();

                        foreach (var item in projectInstance.GetItems(itemName))
                        {
                            writer.WriteStartObject();
                            writer.WriteString("Identity", item.GetMetadataValue("Identity"));

                            foreach (var metadatumName in item.MetadataNames)
                            {
                                if (metadatumName.Equals("Identity", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                writer.WriteString(metadatumName, item.GetMetadataValue(metadatumName));
                            }

                            writer.WriteEndObject();
                        }

                        writer.WriteEndArray();
                    }

                    writer.WriteEndObject();
                }

                if (MSBuildArgs.GetTargetResult is [_, ..])
                {
                    Debug.Assert(buildOrRestoreResult != null);

                    writer.WritePropertyName("TargetResults");
                    writer.WriteStartObject();

                    foreach (var targetName in MSBuildArgs.GetTargetResult)
                    {
                        var targetResult = buildOrRestoreResult.ResultsByTarget[targetName];

                        writer.WritePropertyName(targetName);
                        writer.WriteStartObject();
                        writer.WriteString("Result", targetResult.TargetResultCodeToString());
                        writer.WritePropertyName("Items");
                        writer.WriteStartArray();

                        foreach (var item in targetResult.Items)
                        {
                            writer.WriteStartObject();
                            writer.WriteString("Identity", item.GetMetadata("Identity"));

                            foreach (string metadatumName in item.MetadataNames)
                            {
                                if (metadatumName.Equals("Identity", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                writer.WriteString(metadatumName, item.GetMetadata(metadatumName));
                            }

                            writer.WriteEndObject();
                        }

                        writer.WriteEndArray();
                        writer.WriteEndObject();
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                writer.Flush();
                stream.Write(Encoding.UTF8.GetBytes(Environment.NewLine));
            }
        }
    }

    /// <summary>
    /// Common info needed by <see cref="ComputeCacheEntry"/> but also later stages.
    /// </summary>
    public sealed class CacheInfo
    {
        public required FileInfo EntryPointFile { get; init; }

        /// <summary>
        /// If <see cref="PreviousEntry"/> is <see langword="null"/> and this is
        /// <see langword="true"/>, it means previous entry was deserialized
        /// unsuccessfully (so no need to try again).
        /// </summary>
        public bool TriedDeserializingPreviousEntry { get; set; }

        public RunFileBuildCacheEntry? PreviousEntry { get; set; }
        public required RunFileBuildCacheEntry CurrentEntry { get; init; }

        /// <summary>
        /// The first of <see cref="CurrentEntry"/>'s <see cref="RunFileBuildCacheEntry.ImplicitBuildFiles"/>
        /// which is from the set of MSBuild <see cref="s_implicitBuildFiles"/>.
        /// </summary>
        public string? ExampleMSBuildFile { get; set; }

        /// <summary>
        /// We cannot reuse auxiliary files like <c>csc.rsp</c> for example when SDK version changes.
        /// </summary>
        /// <remarks>
        /// Only set during <see cref="NeedsToBuild"/> or <see cref="GetBuildLevel"/>.
        /// </remarks>
        public bool InitialCanReuseAuxiliaryFiles { get; set; } = true;

        /// <summary>
        /// Set during <see cref="NeedsToBuild"/>.
        /// </summary>
        public bool CanUseCscViaPreviousArguments { get; set; }

        public bool DetermineFinalCanReuseAuxiliaryFiles()
        {
            if (PreviousEntry?.CscArguments.IsDefaultOrEmpty == false)
            {
                return false;
            }

            if (!InitialCanReuseAuxiliaryFiles)
            {
                Reporter.Verbose.WriteLine("CSC auxiliary files can NOT be reused due to the same reason build is needed.");
                return false;
            }

            if (PreviousEntry?.BuildLevel != BuildLevel.Csc)
            {
                Reporter.Verbose.WriteLine("CSC auxiliary files can NOT be reused because previous build level was not CSC " +
                    $"(it was {PreviousEntry?.BuildLevel.ToString() ?? "N/A"}).");
                return false;
            }

            Reporter.Verbose.WriteLine("CSC auxiliary files can be reused.");
            return true;
        }
    }

    /// <summary>
    /// Compute current cache entry - we need to do this always:
    /// <list type="bullet">
    /// <item>if we can skip build, we still need to check everything in the cache entry (e.g., implicit build files)</item>
    /// <item>if we have to build, we need to have the cache entry to write it to the success cache file</item>
    /// </list>
    /// </summary>
    private CacheInfo ComputeCacheEntry()
    {
        var cacheEntry = new RunFileBuildCacheEntry(MSBuildArgs.GlobalProperties?.ToDictionary(StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        {
            Directives = Directives
                .Where(static d => d is not CSharpDirective.Shebang)
                .Select(static d => d.ToString())
                .ToImmutableArray(),
            SdkVersion = Product.Version,
            RuntimeVersion = CSharpCompilerCommand.RuntimeVersion,
        };

        var entryPointFile = new FileInfo(Builder.EntryPointFileFullPath);

        // Collect current implicit build files.
        CollectImplicitBuildFiles(entryPointFile.Directory, cacheEntry.ImplicitBuildFiles, out var exampleMSBuildFile);

        return new CacheInfo
        {
            EntryPointFile = entryPointFile,
            CurrentEntry = cacheEntry,
            ExampleMSBuildFile = exampleMSBuildFile,
        };
    }

    // internal for testing
    internal static void CollectImplicitBuildFiles(DirectoryInfo? startDirectory, HashSet<string> collectedPaths, out string? exampleMSBuildFile)
    {
        Debug.Assert(startDirectory != null);
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

    private bool NeedsToBuild(out CacheInfo cache)
    {
        cache = ComputeCacheEntry();

        if (Directives.Any(static d => d is CSharpDirective.Project))
        {
            Reporter.Verbose.WriteLine("Building because there are project directives.");
            return true;
        }

        // Check cache files.

        string artifactsDirectory = Builder.ArtifactsPath;
        var successCacheFile = new FileInfo(Path.Join(artifactsDirectory, BuildSuccessCacheFileName));

        if (!successCacheFile.Exists)
        {
            Reporter.Verbose.WriteLine("Building because cache file does not exist: " + successCacheFile.FullName);
            return true;
        }

        var startCacheFile = new FileInfo(Path.Join(artifactsDirectory, BuildStartCacheFileName));
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
        var previousCacheEntry = DeserializeCacheEntry(successCacheFile.FullName);
        cache.TriedDeserializingPreviousEntry = true;
        if (previousCacheEntry is null)
        {
            cache.InitialCanReuseAuxiliaryFiles = false;
            Reporter.Verbose.WriteLine("Building because previous cache entry could not be deserialized: " + successCacheFile.FullName);
            return true;
        }

        cache.PreviousEntry = previousCacheEntry;
        var cacheEntry = cache.CurrentEntry;

        // Check that versions match.

        if (previousCacheEntry.SdkVersion != cacheEntry.SdkVersion)
        {
            cache.InitialCanReuseAuxiliaryFiles = false;
            Reporter.Verbose.WriteLine($"""
                Building because previous SDK version ({previousCacheEntry.SdkVersion}) does not match current ({cacheEntry.SdkVersion}): {successCacheFile.FullName}
                """);
            return true;
        }

        if (previousCacheEntry.RuntimeVersion != cacheEntry.RuntimeVersion)
        {
            cache.InitialCanReuseAuxiliaryFiles = false;
            Reporter.Verbose.WriteLine($"""
                Building because previous runtime version ({previousCacheEntry.RuntimeVersion}) does not match current ({cacheEntry.RuntimeVersion}): {successCacheFile.FullName}
                """);
            return true;
        }

        // Check that properties match.

        if (previousCacheEntry.GlobalProperties.Count != cacheEntry.GlobalProperties.Count)
        {
            Reporter.Verbose.WriteLine($"""
                Building because previous global properties count ({previousCacheEntry.GlobalProperties.Count}) does not match current count ({cacheEntry.GlobalProperties.Count}): {successCacheFile.FullName}
                """);
            return true;
        }

        foreach (var (key, value) in cacheEntry.GlobalProperties)
        {
            if (!previousCacheEntry.GlobalProperties.TryGetValue(key, out var otherValue) ||
                value != otherValue)
            {
                Reporter.Verbose.WriteLine($"""
                    Building because previous global property "{key}" ({otherValue}) does not match current ({value}): {successCacheFile.FullName}
                    """);
                return true;
            }
        }

        var entryPointFile = cache.EntryPointFile;

        // If the source file does not exist, we want to build so proper errors are reported.
        if (!entryPointFile.Exists)
        {
            Reporter.Verbose.WriteLine("Building because entry point file is missing: " + entryPointFile.FullName);
            return true;
        }

        var reasonToNotReuseCscArguments = GetReasonToNotReuseCscArguments(cache);
        var targetFile = ResolveLinkTargetOrSelf(entryPointFile);

        // Check that the source file is not modified.
        // Only do this here if we cannot reuse CSC arguments (then checking this first is faster); otherwise we need to check implicit build files anyway.
        if (reasonToNotReuseCscArguments != null && targetFile.LastWriteTimeUtc > buildTimeUtc)
        {
            Reporter.Verbose.WriteLine("Compiling because entry point file is modified: " + targetFile.FullName);
            Reporter.Verbose.WriteLine(reasonToNotReuseCscArguments);
            return true;
        }

        // Check that implicit build files are not modified.
        foreach (var implicitBuildFilePath in previousCacheEntry.ImplicitBuildFiles)
        {
            var implicitBuildFileInfo = ResolveLinkTargetOrSelf(new FileInfo(implicitBuildFilePath));
            if (!implicitBuildFileInfo.Exists || implicitBuildFileInfo.LastWriteTimeUtc > buildTimeUtc)
            {
                Reporter.Verbose.WriteLine("Building because implicit build file is missing or modified: " + implicitBuildFileInfo.FullName);
                return true;
            }
        }

        // Check that no new implicit build files are present.
        foreach (var implicitBuildFilePath in cacheEntry.ImplicitBuildFiles)
        {
            if (!previousCacheEntry.ImplicitBuildFiles.Contains(implicitBuildFilePath))
            {
                Reporter.Verbose.WriteLine("Building because new implicit build file is present: " + implicitBuildFilePath);
                return true;
            }
        }

        // If we might be able to reuse CSC arguments, check whether the source file is modified.
        // NOTE: This must be the last check (otherwise setting cache.CanUseCscViaPreviousArguments would be incorrect).
        if (reasonToNotReuseCscArguments == null && targetFile.LastWriteTimeUtc > buildTimeUtc)
        {
            cache.CanUseCscViaPreviousArguments = true;
            Reporter.Verbose.WriteLine("Compiling because entry point file is modified: " + targetFile.FullName);
            return true;
        }

        return false;

        static FileSystemInfo ResolveLinkTargetOrSelf(FileSystemInfo fileSystemInfo)
        {
            if (!fileSystemInfo.Exists)
            {
                return fileSystemInfo;
            }

            return fileSystemInfo.ResolveLinkTarget(returnFinalTarget: true) ?? fileSystemInfo;
        }

        static string? GetReasonToNotReuseCscArguments(CacheInfo cache)
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
            else
            {
                return null;
            }
        }
    }

    private static RunFileBuildCacheEntry? DeserializeCacheEntry(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize(stream, RunFileJsonSerializerContext.Default.RunFileBuildCacheEntry);
        }
        catch (Exception e)
        {
            Reporter.Verbose.WriteLine($"Failed to deserialize cache entry ({path}): {e.GetType().FullName}: {e.Message}");
            return null;
        }
    }

    public RunFileBuildCacheEntry? GetPreviousCacheEntry()
    {
        return DeserializeCacheEntry(Path.Join(Builder.ArtifactsPath, BuildSuccessCacheFileName));
    }

    private void EnsurePreviousCacheEntry(CacheInfo cache)
    {
        if (cache.PreviousEntry is null && !cache.TriedDeserializingPreviousEntry)
        {
            cache.PreviousEntry = GetPreviousCacheEntry();
            cache.TriedDeserializingPreviousEntry = true;
        }
    }

    private BuildLevel GetBuildLevel(out CacheInfo cache)
    {
        if (!NeedsToBuild(out cache))
        {
            Reporter.Verbose.WriteLine("No need to build, the output is up to date. Cache: " + Builder.ArtifactsPath);
            return BuildLevel.None;
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
        var cacheEntry = cache.CurrentEntry;

        if (!cacheEntry.Directives.IsDefaultOrEmpty)
        {
            Reporter.Verbose.WriteLine("Using MSBuild because there are directives in the source file.");
            return BuildLevel.All;
        }

        var globalProperties = cacheEntry.GlobalProperties.Keys.Except(s_ignorableProperties, cacheEntry.GlobalProperties.Comparer);
        if (globalProperties.FirstOrDefault() is { } exampleKey)
        {
            var exampleValue = cacheEntry.GlobalProperties[exampleKey];
            Reporter.Verbose.WriteLine($"Using MSBuild because there are global properties, for example '{exampleKey}={exampleValue}'.");
            return BuildLevel.All;
        }

        if (cache.ExampleMSBuildFile is { } exampleMSBuildFile)
        {
            Debug.Assert(cacheEntry.ImplicitBuildFiles.Count != 0);
            Reporter.Verbose.WriteLine($"Using MSBuild because there are implicit build files, for example '{exampleMSBuildFile}'.");
            return BuildLevel.All;
        }

        foreach (var filePath in CSharpCompilerCommand.GetPathsOfCscInputsFromNuGetCache())
        {
            if (!File.Exists(filePath))
            {
                Reporter.Verbose.WriteLine($"Using MSBuild because NuGet package file does not exist: {filePath}");
                return BuildLevel.All;
            }
        }

        Reporter.Verbose.WriteLine("Skipping MSBuild and using CSC only.");

        // Don't reuse CSC arguments, this is the "simple" CSC-only build (the one where we use hard-coded CSC arguments).
        if (cache.PreviousEntry != null)
        {
            // If we re-used CSC arguments in previous run and
            // want to use hard-coded CSC arguments in this run,
            // we cannot reuse the csc.rsp file.
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
    /// Touching the artifacts folder ensures it's considered as recently used and not cleaned up by <see cref="CleanFileBasedAppArtifactsCommand"/>.
    /// </summary>
    public void MarkArtifactsFolderUsed()
    {
        if (NoWriteBuildMarkers)
        {
            return;
        }

        string directory = Builder.ArtifactsPath;

        try
        {
            Directory.SetLastWriteTimeUtc(directory, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            Reporter.Verbose.WriteLine($"Cannot touch folder '{directory}': {ex}");
        }
    }

    private void MarkBuildStart()
    {
        if (NoWriteBuildMarkers)
        {
            return;
        }

        string directory = Builder.ArtifactsPath;

        CreateTempSubdirectory(directory);

        MarkArtifactsFolderUsed();

        File.WriteAllText(Path.Join(directory, BuildStartCacheFileName), Builder.EntryPointFileFullPath);
    }

    private void MarkBuildSuccess(CacheInfo cache)
    {
        if (NoWriteBuildMarkers)
        {
            return;
        }

        string successCacheFile = Path.Join(Builder.ArtifactsPath, BuildSuccessCacheFileName);
        using var stream = File.Open(successCacheFile, FileMode.Create, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, cache.CurrentEntry, RunFileJsonSerializerContext.Default.RunFileBuildCacheEntry);
    }

    public ProjectInstance CreateProjectInstance(ProjectCollection projectCollection)
    {
        return CreateProjectInstance(projectCollection, addGlobalProperties: null);
    }

    public ProjectInstance CreateProjectInstance(ProjectCollection projectCollection, Action<IDictionary<string, string>>? addGlobalProperties)
    {
        Builder.CreateProjectInstance(
            projectCollection,
            ThrowingReporter,
            out var project,
            out var evaluatedDirectives,
            Directives,
            addGlobalProperties);

        Directives = evaluatedDirectives;

        return project;
    }

    /// <summary>
    /// Creates a temporary subdirectory for file-based apps.
    /// Use <see cref="GetTempSubpath"/> to obtain the path.
    /// </summary>
    public static void CreateTempSubdirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            // Ensure only the current user has access to the directory to avoid leaking the program to other users.
            // We don't mind that permissions might be different if the directory already exists,
            // since it's under user's local directory and its path should be unique.
            Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    public static readonly ErrorReporter ThrowingReporter =
        static (sourceFile, textSpan, message) => throw new GracefulException($"{sourceFile.GetLocationString(textSpan)}: {FileBasedProgramsResources.DirectiveError}: {message}");
}

internal sealed class RunFileBuildCacheEntry
{
    private static StringComparer GlobalPropertiesComparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// We can't know which parts of the path are case insensitive, so we are conservative
    /// to avoid false positives in the cache (saying we are up to date even if we are not).
    /// </summary>
    private static StringComparer FilePathComparer => StringComparer.Ordinal;

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, string> GlobalProperties { get; }

    /// <summary>
    /// Full paths.
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public HashSet<string> ImplicitBuildFiles { get; }

    /// <summary>
    /// <see cref="CSharpDirective"/>s recognized by the SDK (i.e., except shebang).
    /// </summary>
    public ImmutableArray<string> Directives { get; set; } = [];

    public BuildLevel BuildLevel { get; set; }

    public string? SdkVersion { get; set; } // should be required and init-only but https://github.com/dotnet/runtime/issues/92877

    public string? RuntimeVersion { get; set; } // should be required and init-only but https://github.com/dotnet/runtime/issues/92877

    public RunProperties? Run { get; set; }

    /// <summary>
    /// <see cref="CSharpCompilerCommand.CscArguments"/>
    /// </summary>
    public ImmutableArray<string> CscArguments { get; set; } = [];

    /// <summary>
    /// <see cref="CSharpCompilerCommand.BuildResultFile"/>
    /// </summary>
    public string? BuildResultFile { get; set; }

    [JsonConstructor]
    public RunFileBuildCacheEntry()
    {
        GlobalProperties = new(GlobalPropertiesComparer);
        ImplicitBuildFiles = new(FilePathComparer);
    }

    public RunFileBuildCacheEntry(Dictionary<string, string> globalProperties)
    {
        Debug.Assert(globalProperties.Comparer == GlobalPropertiesComparer);
        GlobalProperties = globalProperties;
        ImplicitBuildFiles = new(FilePathComparer);
    }
}

[JsonSerializable(typeof(RunFileBuildCacheEntry))]
[JsonSerializable(typeof(RunFileArtifactsMetadata))]
internal partial class RunFileJsonSerializerContext : JsonSerializerContext;

internal enum BuildLevel
{
    /// <summary>
    /// No build is necessary, build outputs are up to date wrt. inputs.
    /// </summary>
    None,

    /// <summary>
    /// Only C# files are modified and there are no SDK-recognized <see cref="CSharpDirective"/>s.
    /// We can invoke just the C# compiler to get up to date.
    /// </summary>
    Csc,

    /// <summary>
    /// We need to invoke MSBuild to get up to date.
    /// </summary>
    All,
}

[Flags]
internal enum AppKinds
{
    None = 0,
    ProjectBased = 1 << 0,
    FileBased = 1 << 1,
    Any = ProjectBased | FileBased,
}
