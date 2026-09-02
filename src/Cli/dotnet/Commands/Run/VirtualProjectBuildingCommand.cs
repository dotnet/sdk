// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
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

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Used to build a virtual project file in memory to support <c>dotnet run file.cs</c>.
/// </summary>
internal sealed class VirtualProjectBuildingCommand : CommandBase
{
    internal const string FileBasedProgramCanSkipMSBuild = nameof(FileBasedProgramCanSkipMSBuild);

    public static string TargetFrameworkVersion => Product.TargetFrameworkVersion;
    public static string TargetFramework => $"net{Product.TargetFrameworkVersion}";

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
    public (BuildLevel Level, FileBasedAppCacheInfo? Cache) LastBuild { get; private set; }

    /// <summary>
    /// Filled during <see cref="Execute"/>.
    /// </summary>
    public RunProperties? LastRunProperties { get; private set; }

    /// <summary>
    /// If <see langword="true"/>, no build markers are written
    /// (like <see cref="FileBasedAppRunPlan.BuildStartCacheFileName"/> and <see cref="FileBasedAppRunPlan.BuildSuccessCacheFileName"/>).
    /// Also skips automatic cleanup.
    /// This property does not control whether the markers are checked, use <see cref="NoCache"/> for that.
    /// </summary>
    public bool NoWriteBuildMarkers { get; init; }

    public bool NoConsoleLogger { get; init; }

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

        set
        {
            field = value;
            EvaluatedDirectives = default;
        }
    }

    public ImmutableArray<CSharpDirective> EvaluatedDirectives { get; private set; }

    public VirtualProjectBuildingCommand(
        string entryPointFileFullPath,
        MSBuildArgs msbuildArgs,
        string? artifactsPath = null)
    {
        MSBuildArgs = msbuildArgs.CloneWithAdditionalProperties(
            CommonRunHelpers.CreateFileBasedRunGlobalProperties().AsReadOnly());

        NoConsoleLogger = LoggerUtility.HasNoConsoleLoggerArgument(MSBuildArgs.OtherMSBuildArgs);

        Builder = new VirtualProjectBuilder(BuildService.Instance, entryPointFileFullPath, TargetFramework, MSBuildArgs.GetResolvedTargets(), artifactsPath);
    }

#if !CLI_AOT
    [UnconditionalSuppressMessage("AOT", "IL2026", Justification = "Temporary unblock for dotnet/msbuild#14064 (MSBuild build APIs are now [RequiresUnreferencedCode]). dotnet CLI runs MSBuild in-proc (not trimmed). Remove when dotnet/sdk#55225 is fixed.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification ="In non-AOT mode we have MSBuild available, so using types from it is safe.")]
    public override int Execute()
    {
        bool msbuildGet = MSBuildArgs.GetProperty is [_, ..] || MSBuildArgs.GetItem is [_, ..] || MSBuildArgs.GetTargetResult is [_, ..];
        bool evalOnly = msbuildGet && Builder.RequestedTargets is null or [];
        bool minimizeStdOut = msbuildGet && MSBuildArgs.GetResultOutputFile is null or [];

        var verbosity = MSBuildArgs.Verbosity ?? MSBuildForwardingAppWithoutLogging.DefaultVerbosity;
        var consoleLogger = NoConsoleLogger
            ? null
            : minimizeStdOut
            ? new SimpleErrorLogger()
            : CommonRunHelpers.GetConsoleLogger(MSBuildArgs.CloneWithExplicitArgs([$"--verbosity:{verbosity}", .. MSBuildArgs.OtherMSBuildArgs]));
        var binaryLogger = GetBinaryLogger(MSBuildArgs.OtherMSBuildArgs);

        FileBasedAppCacheInfo? cache = null;

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
            var buildLevel = GetBuildLevel(out cache);
            cache?.CurrentEntry.BuildLevel = buildLevel;
            LastBuild = (buildLevel, cache);

            if (buildLevel is BuildLevel.None)
            {
                if (binaryLogger is not null)
                {
                    Reporter.Output.WriteLine(CliCommandStrings.NoBinaryLogBecauseUpToDate.Yellow());
                }

                // No rebuild, can reuse run properties.
                cache?.CurrentEntry.Run = cache.PreviousEntry?.Run;

                MarkArtifactsFolderUsed();
                return 0;
            }

            if (buildLevel is BuildLevel.Csc)
            {
                Debug.Assert(cache is not null);

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
                        ReuseInfoFromPreviousCacheEntry(cache);
                        MarkBuildSuccess(cache);
                    }

                    if (binaryLogger is not null)
                    {
                        Reporter.Output.WriteLine(CliCommandStrings.NoBinaryLogBecauseRunningJustCsc.Yellow());
                    }

                    return result;
                }

                Debug.Assert(result != 0);

                buildLevel = BuildLevel.All;
                cache.CurrentEntry.BuildLevel = buildLevel;
                LastBuild = (buildLevel, cache);
            }

            Debug.Assert(buildLevel is BuildLevel.All or BuildLevel.Csc);

            MarkBuildStart();
        }

        if (!NoWriteBuildMarkers && !msbuildGet)
        {
            CleanFileBasedAppArtifactsCommand.StartAutomaticCleanupIfNeeded();
        }

        IDisposable? environmentScope = null;
        try
        {
            environmentScope = MSBuildForwardingAppWithoutLogging.SetMSBuildRequiredEnvironmentVariables();

            // Set up MSBuild.
            ReadOnlySpan<ILogger> binaryLoggers = binaryLogger is null ? [] : [binaryLogger.Value];
            ReadOnlySpan<ILogger> consoleLoggers = consoleLogger is null ? [] : [consoleLogger];
            IEnumerable<ILogger> loggers = [.. binaryLoggers, .. consoleLoggers];
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
                    CreateProjectInstance(projectCollection, additionalGlobalProperties: GetAdditionalRestoreGlobalProperties(MSBuildArgs.RestoreGlobalProperties)),
                    targetsToBuild: ["Restore"],
                    hostServices: null,
                    // We don't include ClearCachesAfterBuild flag unlike MSBuild's implicit restore
                    // to avoid evicting the virtual project asynchronously (https://github.com/dotnet/msbuild/issues/14148).
                    // It shouldn't make a difference for us because the restore has distinct global properties, so all projects will be re-evaluated anyway.
                    BuildRequestDataFlags.SkipNonexistentTargets | BuildRequestDataFlags.IgnoreMissingEmptyAndInvalidImports | BuildRequestDataFlags.FailOnUnresolvedSdk);

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
                var effectiveTargets = Builder.RequestedTargets is { Length: > 0 } requestedTargets
                    ? requestedTargets
                    : [Constants.Build, Constants.CoreCompile];
                var buildRequest = new BuildRequestData(
                    CreateProjectInstance(projectCollection),
                    targetsToBuild: effectiveTargets,
                    hostServices: null,
                    // SkipNonexistentTargets: CoreCompile doesn't exist in the outer build of multi-target projects.
                    BuildRequestDataFlags.SkipNonexistentTargets);

                var buildResult = BuildManager.DefaultBuildManager.BuildRequest(buildRequest);
                if (buildResult.OverallResult != BuildResultCode.Success)
                {
                    exitCode = 1;
                }

                if (exitCode == 0 && !msbuildGet)
                {
                    Debug.Assert(buildRequest.ProjectInstance != null);

                    // Cache run info (to avoid re-evaluating the project instance).
                    LastRunProperties = RunProperties.TryFromProject(buildRequest.ProjectInstance, out var runProperties)
                        ? runProperties
                        : null;

                    if (cache is not null && CanSaveCache(buildRequest.ProjectInstance))
                    {
                        cache.CurrentEntry.Run = LastRunProperties;

                        CacheCscArguments(cache, buildResult);
                        WriteCscRsp(cache);
                        CollectAdditionalSources(cache, buildRequest.ProjectInstance);

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
            environmentScope?.Dispose();
            if (binaryLogger?.IsValueCreated == true) binaryLogger.Value.ReallyShutdown();
            consoleLogger?.Shutdown();
        }

        static IDictionary<string, string> GetAdditionalRestoreGlobalProperties(ReadOnlyDictionary<string, string>? restoreProperties)
        {
            // Compute the session ID outside the lambda to ensure it's the same for all project instances
            // (since there can be multiple project instances created while evaluating file-level directives).
            var sessionId = Guid.NewGuid().ToString("D");

            var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MSBuildRestoreSessionId"] = sessionId,
                ["MSBuildIsRestoring"] = bool.TrueString,
            };

            foreach (var (key, value) in RestoringCommand.RestoreOptimizationProperties)
            {
                globalProperties[key] = value;
            }

            if (restoreProperties != null)
            {
                foreach (var (key, value) in restoreProperties)
                {
                    if (value is not null)
                    {
                        globalProperties[key] = value;
                    }
                }
            }

            return globalProperties;
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

        void CacheCscArguments(FileBasedAppCacheInfo cache, BuildResult result)
        {
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

        void ReuseInfoFromPreviousCacheEntry(FileBasedAppCacheInfo cache)
        {
            Debug.Assert(cache.CurrentEntry.AdditionalSources.Count == 0);

            if (cache.PreviousEntry != null)
            {
                foreach (var file in cache.PreviousEntry.AdditionalSources)
                {
                    cache.CurrentEntry.AdditionalSources.Add(file);
                }
            }
        }

        void WriteCscRsp(FileBasedAppCacheInfo cache)
        {
            if (cache.CurrentEntry.CscArguments.IsDefaultOrEmpty)
            {
                return;
            }

            string rspPath = CSharpCompilerCommand.WriteCscRspFile(Builder.ArtifactsPath, cache.CurrentEntry.CscArguments);
            Reporter.Verbose.WriteLine($"Wrote '{rspPath}'.");
        }

        bool CanSaveCache(ProjectInstance projectInstance)
        {
            if (!MSBuildUtilities.ConvertStringToBool(projectInstance.GetPropertyValue(FileBasedProgramCanSkipMSBuild), defaultValue: true))
            {
                Reporter.Verbose.WriteLine($"Not saving cache because there is an opt-out via MSBuild property {FileBasedProgramCanSkipMSBuild}.");
                return false;
            }

            if (EvaluatedDirectives.Any(static d => d is CSharpDirective.Project))
            {
                Reporter.Verbose.WriteLine("Not saving cache because there is a project directive.");
                return false;
            }

            if (EvaluatedDirectives.Any(static d => d is CSharpDirective.Ref))
            {
                Reporter.Verbose.WriteLine("Not saving cache because there is a ref directive.");
                return false;
            }

            if (EvaluatedDirectives.Any(static d =>
                    d is CSharpDirective.IncludeOrExclude { Kind: CSharpDirective.IncludeOrExcludeKind.Include } includeDirective &&
                    includeDirective.Name.AsSpan().ContainsAny('*', '?')))
            {
                Reporter.Verbose.WriteLine("Not saving cache because there is a glob include directive.");
                return false;
            }

            return true;
        }

        [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
        void CollectAdditionalSources(FileBasedAppCacheInfo cache, ProjectInstance projectInstance)
        {
            Debug.Assert(cache.CurrentEntry.AdditionalSources.Count == 0);

            var entryPointFileDirectory = Path.GetDirectoryName(Builder.EntryPointFileFullPath);
            Debug.Assert(entryPointFileDirectory != null);

            var mapping = Builder.GetItemMappingAsync(projectInstance.Wrap(), ErrorReporters.IgnoringReporter).AsTask().GetAwaiter().GetResult();
            foreach (var entry in mapping)
            {
                if (string.Equals(entry.ItemType, "None", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var item in projectInstance.GetItems(entry.ItemType))
                {
                    var fullPath = Path.GetFullPath(
                        path: item.GetMetadataValue("FullPath"),
                        basePath: entryPointFileDirectory);

                    cache.CurrentEntry.AdditionalSources.Add(RunFileBuildCacheEntry.AdditionalSource.Create(fullPath));
                }
            }

            cache.CurrentEntry.AdditionalSources.Remove(RunFileBuildCacheEntry.AdditionalSource.Create(Builder.EntryPointFileFullPath));
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

#endif

    private RunFileBuildCacheEntry? GetPreviousCacheEntry()
    {
        return FileBasedAppRunPlan.ReadCacheEntry(
            Path.Join(Builder.ArtifactsPath, FileBasedAppRunPlan.BuildSuccessCacheFileName));
    }

    private void EnsurePreviousCacheEntry(FileBasedAppCacheInfo cache)
    {
        if (cache.PreviousEntry is null && !cache.TriedDeserializingPreviousEntry)
        {
            cache.PreviousEntry = GetPreviousCacheEntry();
            cache.TriedDeserializingPreviousEntry = true;
        }
    }

    /// <summary>
    /// Determines the work required to make the virtual project outputs current.
    /// </summary>
    /// <param name="cache">Receives the computed cache state.</param>
    /// <returns>The required build level.</returns>
    public BuildLevel GetBuildLevel(out FileBasedAppCacheInfo? cache)
    {
        ImmutableArray<CSharpDirective> directives = Directives;
        bool canCache = !directives.Any(static directive => directive is CSharpDirective.Project or CSharpDirective.Ref);
        ImmutableArray<string> cacheDirectives = canCache
            ? directives
                .Where(static directive => directive is not CSharpDirective.Shebang)
                .Select(static directive => directive.ToString())
                .ToImmutableArray()
            : [];
        var inputs = new FileBasedAppRunPlanInputs(
            EntryPointFileFullPath: Builder.EntryPointFileFullPath,
            ArtifactsPath: Builder.ArtifactsPath,
            GlobalProperties: canCache
                ? MSBuildArgs.GlobalProperties?.ToDictionary(StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            CanCache: canCache,
            Directives: cacheDirectives,
            SdkVersion: canCache ? Product.Version : string.Empty,
            RuntimeVersion: canCache ? CSharpCompilerCommand.RuntimeVersion : string.Empty,
            NoCache,
            GetCscInputPaths: CSharpCompilerCommand.GetPathsOfCscInputsFromNuGetCache);
        RunPlan plan = FileBasedAppRunPlan.Analyze(inputs);
        cache = plan.Cache;
        return plan.ToBuildLevel();
    }

    public void MarkArtifactsFolderUsed()
    {
        if (NoWriteBuildMarkers)
        {
            return;
        }

        FileBasedAppRunPlan.MarkArtifactsPathUsed(Builder.ArtifactsPath);
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

        File.WriteAllText(Path.Join(directory, FileBasedAppRunPlan.BuildStartCacheFileName), Builder.EntryPointFileFullPath);
    }

    private void MarkBuildSuccess(FileBasedAppCacheInfo cache)
    {
        if (NoWriteBuildMarkers)
        {
            return;
        }

        string successCacheFile = Path.Join(Builder.ArtifactsPath, FileBasedAppRunPlan.BuildSuccessCacheFileName);
        using var stream = File.Open(successCacheFile, FileMode.Create, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, cache.CurrentEntry, RunFileBuildCacheJsonSerializerContext.Default.RunFileBuildCacheEntry);
    }

    [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
    public ProjectInstance CreateProjectInstance(ProjectCollection projectCollection)
    {
        return CreateProjectInstance(projectCollection, additionalGlobalProperties: null);
    }

    [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
    public ProjectInstance CreateProjectInstance(ProjectCollection projectCollection, IDictionary<string, string>? additionalGlobalProperties = null)
    {
        var projectCollectionWrapped = projectCollection.Wrap();

        var result = Builder.CreateProjectInstanceAsync(
            projectCollectionWrapped,
            ThrowingReporter,
            Directives,
            additionalGlobalProperties).AsTask().GetAwaiter().GetResult();

        EvaluatedDirectives = result.EvaluatedDirectives;

        return result.Project.Unwrap();
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
        static (text, path, textSpan, message, innerException) =>
            throw new GracefulException(
            $"{new SourceFile(path, text).GetLocationString(textSpan)}: {FileBasedProgramsResources.DirectiveError}: {message}",
            innerException);

    public static SourceFile RemoveDirectivesFromFile(SourceFile sourceFile)
    {
        var editor = FileBasedAppSourceEditor.Load(sourceFile);

        while (editor.Directives is [{ } directive, ..])
        {
            editor.Remove(directive);
        }

        return editor.SourceFile;
    }

    public static void RemoveDirectivesFromFile(SourceFile sourceFile, string targetFilePath)
    {
        var modifiedFile = RemoveDirectivesFromFile(sourceFile);
        (modifiedFile with { Path = targetFilePath }).Save();
    }
}
