// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

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

    private static readonly EnumerationOptions s_csEnumerationOptions = new()
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        RecurseSubdirectories = true,
    };

    private static readonly ImmutableArray<string> s_implicitBuildFileNames =
    [
        "global.json",

        // All these casings are recognized on case-sensitive platforms:
        // https://github.com/NuGet/NuGet.Client/blob/ab6b96fd9ba07ed3bf629ee389799ca4fb9a20fb/src/NuGet.Core/NuGet.Configuration/Settings/Settings.cs#L32-L37
        "nuget.config",
        "NuGet.config",
        "NuGet.Config",

        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Packages.props",
        "Directory.Build.rsp",
        "MSBuild.rsp",
    ];

    private string? _projectFileText;

    public VirtualProjectBuildingCommand(
        string entryPointFileFullPath,
        string[] msbuildArgs,
        VerbosityOptions? verbosity,
        bool interactive)
    {
        Debug.Assert(Path.IsPathFullyQualified(entryPointFileFullPath));

        EntryPointFileFullPath = entryPointFileFullPath;
        GlobalProperties = new(StringComparer.OrdinalIgnoreCase);
        CommonRunHelpers.AddUserPassedProperties(GlobalProperties, msbuildArgs);
        BinaryLoggerArgs = msbuildArgs;
        Verbosity = verbosity ?? RunCommand.GetDefaultVerbosity(interactive: interactive);
    }

    public string EntryPointFileFullPath { get; }
    public Dictionary<string, string> GlobalProperties { get; }
    public string[] BinaryLoggerArgs { get; }
    public VerbosityOptions Verbosity { get; }
    public bool NoRestore { get; init; }
    public bool NoCache { get; init; }
    public bool NoBuild { get; init; }
    public bool NoIncremental { get; init; }

    public override int Execute()
    {
        Debug.Assert(!(NoRestore && NoBuild));

        var consoleLogger = RunCommand.MakeTerminalLogger(Verbosity);
        var binaryLogger = GetBinaryLogger(BinaryLoggerArgs);

        CacheInfo? cache = null;

        if (!NoBuild)
        {
            if (NoCache)
            {
                if (NoRestore)
                {
                    throw new GracefulException(CliCommandStrings.InvalidOptionCombination, RunCommandParser.NoCacheOption.Name, RunCommandParser.NoRestoreOption.Name);
                }

                cache = ComputeCacheEntry();
            }
            else if (!NeedsToBuild(out cache))
            {
                if (binaryLogger is not null)
                {
                    Reporter.Output.WriteLine(CliCommandStrings.NoBinaryLogBecauseUpToDate.Yellow());
                }

                PrepareProjectInstance(cache);

                return 0;
            }

            MarkBuildStart(cache);
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
            ReadOnlySpan<ILogger> binaryLoggers = binaryLogger is null ? [] : [binaryLogger];
            var projectCollection = new ProjectCollection(
                GlobalProperties,
                [.. binaryLoggers, consoleLogger],
                ToolsetDefinitionLocations.Default);
            var parameters = new BuildParameters(projectCollection)
            {
                Loggers = projectCollection.Loggers,
                LogTaskInputs = binaryLoggers.Length != 0,
            };
            BuildManager.DefaultBuildManager.BeginBuild(parameters);

            PrepareProjectInstance(cache);

            // Do a restore first (equivalent to MSBuild's "implicit restore", i.e., `/restore`).
            // See https://github.com/dotnet/msbuild/blob/a1c2e7402ef0abe36bf493e395b04dd2cb1b3540/src/MSBuild/XMake.cs#L1838
            // and https://github.com/dotnet/msbuild/issues/11519.
            if (!NoRestore)
            {
                var restoreRequest = new BuildRequestData(
                    CreateProjectInstance(projectCollection, addGlobalProperties: static (globalProperties) =>
                    {
                        globalProperties["MSBuildRestoreSessionId"] = Guid.NewGuid().ToString("D");
                        globalProperties["MSBuildIsRestoring"] = bool.TrueString;
                    }),
                    targetsToBuild: ["Restore"],
                    hostServices: null,
                    BuildRequestDataFlags.ClearCachesAfterBuild | BuildRequestDataFlags.SkipNonexistentTargets | BuildRequestDataFlags.IgnoreMissingEmptyAndInvalidImports | BuildRequestDataFlags.FailOnUnresolvedSdk);
                var restoreResult = BuildManager.DefaultBuildManager.BuildRequest(restoreRequest);
                if (restoreResult.OverallResult != BuildResultCode.Success)
                {
                    return 1;
                }
            }

            // Then do a build.
            if (!NoBuild)
            {
                var buildRequest = new BuildRequestData(
                    CreateProjectInstance(projectCollection),
                    targetsToBuild: [NoIncremental ? "Rebuild" : "Build"]);
                var buildResult = BuildManager.DefaultBuildManager.BuildRequest(buildRequest);
                if (buildResult.OverallResult != BuildResultCode.Success)
                {
                    return 1;
                }

                Debug.Assert(cache != null);
                MarkBuildSuccess(cache);
            }

            BuildManager.DefaultBuildManager.EndBuild();

            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
        finally
        {
            foreach (var (key, value) in savedEnvironmentVariables)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            binaryLogger?.Shutdown();
            consoleLogger.Shutdown();
        }

        static ILogger? GetBinaryLogger(string[] args)
        {
            // Like in MSBuild, only the last binary logger is used.
            for (int i = args.Length - 1; i >= 0; i--)
            {
                var arg = args[i];
                if (LoggerUtility.IsBinLogArgument(arg))
                {
                    return new BinaryLogger
                    {
                        Parameters = arg.IndexOf(':') is >= 0 and var index
                            ? arg[(index + 1)..]
                            : "msbuild.binlog",
                    };
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Common info needed by <see cref="ComputeCacheEntry"/> but also later stages.
    /// </summary>
    private sealed class CacheInfo
    {
        public required FileInfo EntryPointFile { get; init; }
        public required string ArtifactsDirectory { get; init; }
        public required FileInfo SuccessCacheFile { get; init; }
        public required RunFileBuildCacheEntry? PreviousEntry { get; init; }
        public required RunFileBuildCacheEntry CurrentEntry { get; init; }
        public required ImmutableArray<OtherSourceFile> OtherSources { get; init; }
    }

    /// <summary>
    /// Compute current <see cref="RunFileBuildCacheEntry"/> - we need to do this always:
    /// <list type="bullet">
    /// <item>if we can skip build, we still need to check everything in the cache entry (e.g., implicit build files)</item>
    /// <item>if we have to build, we need to have the cache entry to write it to the success cache file</item>
    /// </list>
    /// </summary>
    private CacheInfo ComputeCacheEntry()
    {
        string artifactsDirectory = GetArtifactsPath();
        var successCacheFile = new FileInfo(Path.Join(artifactsDirectory, BuildSuccessCacheFileName));
        DateTime? buildTimeUtcOpt = !successCacheFile.Exists ? null : successCacheFile.LastWriteTimeUtc;

        var previousCacheEntry = DeserializeCacheEntry(successCacheFile);

        var cacheEntry = new RunFileBuildCacheEntry(GlobalProperties);
        var entryPointFile = new FileInfo(EntryPointFileFullPath);

        // Collect current other source files.
        var otherSources = ImmutableArray.CreateBuilder<OtherSourceFile>(previousCacheEntry?.OtherSources.Count ?? 8);
        foreach (var item in FindOtherFiles(entryPointFile: entryPointFile, entryDirectory: null))
        {
            OtherSourceFile otherSource;

            // No need to parse the file if it hasn't changed since last time.
            if (buildTimeUtcOpt is { } buildTimeUtc &&
                previousCacheEntry?.OtherSources.TryGetValue(item.File.FullName, out var previous) == true &&
                item.File.LastWriteTimeUtc <= buildTimeUtc)
            {
                otherSource = new OtherSourceFile
                {
                    Path = item.File.FullName,
                    IsTopLevel = item.IsTopLevel,
                    IsEntryPoint = previous.IsEntryPoint,
                };
            }
            else
            {
                otherSource = ToOtherSourceFile(item);
            }

            otherSources.Add(otherSource);
            cacheEntry.OtherSources.Add(item.File.FullName, new() { IsEntryPoint = otherSource.IsEntryPoint });
        }

        // Collect current implicit build files.
        for (DirectoryInfo? directory = entryPointFile.Directory; directory != null; directory = directory.Parent)
        {
            foreach (var implicitBuildFileName in s_implicitBuildFileNames)
            {
                string implicitBuildFilePath = Path.Join(directory.FullName, implicitBuildFileName);
                if (File.Exists(implicitBuildFilePath))
                {
                    cacheEntry.ImplicitBuildFiles.Add(implicitBuildFilePath);
                }
            }
        }

        return new CacheInfo
        {
            EntryPointFile = entryPointFile,
            ArtifactsDirectory = artifactsDirectory,
            SuccessCacheFile = successCacheFile,
            PreviousEntry = previousCacheEntry,
            CurrentEntry = cacheEntry,
            OtherSources = otherSources.DrainToImmutable(),
        };

        static RunFileBuildCacheEntry? DeserializeCacheEntry(FileInfo cacheFile)
        {
            try
            {
                using var stream = File.Open(cacheFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                return JsonSerializer.Deserialize(stream, RunFileJsonSerializerContext.Default.RunFileBuildCacheEntry);
            }
            catch (Exception e)
            {
                Reporter.Verbose.WriteLine($"Failed to deserialize cache entry ({cacheFile.FullName}): {e.GetType().FullName}: {e.Message}");
                return null;
            }
        }
    }

    private bool NeedsToBuild(out CacheInfo cache)
    {
        cache = ComputeCacheEntry();

        // Check cache files.

        string artifactsDirectory = cache.ArtifactsDirectory;
        var successCacheFile = cache.SuccessCacheFile;

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

        var previousCacheEntry = cache.PreviousEntry;
        if (previousCacheEntry is null)
        {
            Reporter.Verbose.WriteLine("Building because previous cache entry could not be deserialized: " + successCacheFile.FullName);
            return true;
        }

        // Check that properties match.

        var currentEntry = cache.CurrentEntry;
        if (previousCacheEntry.GlobalProperties.Count != currentEntry.GlobalProperties.Count)
        {
            Reporter.Verbose.WriteLine($"""
                Building because previous global properties count ({previousCacheEntry.GlobalProperties.Count}) does not match current count ({currentEntry.GlobalProperties.Count}): {successCacheFile.FullName}
                """);
            return true;
        }

        foreach (var (key, value) in currentEntry.GlobalProperties)
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

        // Check that the entry-point file is up to date.
        // If it does not exist, we also want to build.
        var entryPointFile = cache.EntryPointFile;
        if (!entryPointFile.Exists || entryPointFile.LastWriteTimeUtc > buildTimeUtc)
        {
            Reporter.Verbose.WriteLine("Building because entry point file is missing or modified: " + entryPointFile.FullName);
            return true;
        }

        // Check that other source files are up to date.
        foreach (var (otherSourceFilePath, otherSource) in previousCacheEntry.OtherSources)
        {
            if (!otherSource.IsEntryPoint)
            {
                var otherSourceFile = new FileInfo(otherSourceFilePath);
                if (!otherSourceFile.Exists || otherSourceFile.LastWriteTimeUtc > buildTimeUtc)
                {
                    Reporter.Verbose.WriteLine("Building because other source file is missing or modified: " + otherSourceFile.FullName);
                    return true;
                }
            }
        }

        // Check that no new other source files are present.
        foreach (var (otherSourceFilePath, otherSource) in currentEntry.OtherSources)
        {
            if (!otherSource.IsEntryPoint && !previousCacheEntry.OtherSources.ContainsKey(otherSourceFilePath))
            {
                Reporter.Verbose.WriteLine("Building because new other source file is present: " + otherSourceFilePath);
                return true;
            }
        }

        // Check that implicit build files are up to date.
        foreach (var implicitBuildFilePath in previousCacheEntry.ImplicitBuildFiles)
        {
            var implicitBuildFileInfo = new FileInfo(implicitBuildFilePath);
            if (!implicitBuildFileInfo.Exists || implicitBuildFileInfo.LastWriteTimeUtc > buildTimeUtc)
            {
                Reporter.Verbose.WriteLine("Building because implicit build file is missing or modified: " + implicitBuildFileInfo.FullName);
                return true;
            }
        }

        // Check that no new implicit build files are present.
        foreach (var implicitBuildFilePath in currentEntry.ImplicitBuildFiles)
        {
            if (!previousCacheEntry.ImplicitBuildFiles.Contains(implicitBuildFilePath))
            {
                Reporter.Verbose.WriteLine("Building because new implicit build file is present: " + implicitBuildFilePath);
                return true;
            }
        }

        // If everything is up to date, reuse project file text.
        currentEntry.ProjectFileText = previousCacheEntry?.ProjectFileText;

        return false;
    }

    private static void MarkBuildStart(CacheInfo cache)
    {
        string directory = cache.ArtifactsDirectory;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Join(directory, BuildStartCacheFileName), cache.EntryPointFile.FullName);
    }

    private static void MarkBuildSuccess(CacheInfo cache)
    {
        string successCacheFile = Path.Join(cache.ArtifactsDirectory, BuildSuccessCacheFileName);
        using var stream = File.Open(successCacheFile, FileMode.Create, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, cache.CurrentEntry, RunFileJsonSerializerContext.Default.RunFileBuildCacheEntry);
    }

    /// <summary>
    /// Needs to be called before the first call to <see cref="CreateProjectInstance(ProjectCollection)"/>.
    /// </summary>
    public VirtualProjectBuildingCommand PrepareProjectInstance() => PrepareProjectInstance(cache: null);

    private VirtualProjectBuildingCommand PrepareProjectInstance(CacheInfo? cache)
    {
        Debug.Assert(_projectFileText == null, $"{nameof(PrepareProjectInstance)} should not be called multiple times.");

        if (cache?.CurrentEntry.ProjectFileText is { } projectFileText)
        {
            _projectFileText = projectFileText;
            return this;
        }

        DiscoverOtherFiles(
            entryPointFile: LoadSourceFile(EntryPointFileFullPath),
            entryDirectory: null,
            cache,
            parseDirectivesFromOtherEntryPoints: false,
            reportAllDirectiveErrors: false,
            directiveErrors: null,
            otherEntryPoints: out var otherEntryPoints,
            parsedFiles: out var parsedFiles);

        var csprojWriter = new StringWriter();

        WriteProjectFile(
            writer: csprojWriter,
            directives: parsedFiles[EntryPointFileFullPath].SortedDirectives,
            options: new ProjectWritingOptions.Virtual
            {
                ArtifactsPath = GetArtifactsPath(),
                ExcludeCompileItems = otherEntryPoints,
            });

        projectFileText = csprojWriter.ToString();
        cache?.CurrentEntry.ProjectFileText = projectFileText;
        _projectFileText = projectFileText;

        return this;
    }

    /// <param name="parseDirectivesFromOtherEntryPoints">
    /// Whether <paramref name="parsedFiles"/> should contain other entry points
    /// or just the current one (<paramref name="entryPointFile"/>).
    /// </param>
    /// <param name="otherEntryPoints">
    /// Full paths.
    /// </param>
    /// <param name="parsedFiles">
    /// For a source file full path, contains loaded text and parsed directives.
    /// Contains other entry points only if <paramref name="parseDirectivesFromOtherEntryPoints"/> is set to <see langword="true"/>.
    /// </param>
    public static void DiscoverOtherFiles(
        SourceFile? entryPointFile,
        DirectoryInfo? entryDirectory,
        bool parseDirectivesFromOtherEntryPoints,
        bool reportAllDirectiveErrors,
        ImmutableArray<SimpleDiagnostic>.Builder? directiveErrors,
        out ImmutableArray<string> otherEntryPoints,
        out IReadOnlyDictionary<string, ParsedSourceFile> parsedFiles)
    {
        DiscoverOtherFiles(
            entryPointFile: entryPointFile,
            entryDirectory: entryDirectory,
            cache: null,
            parseDirectivesFromOtherEntryPoints: parseDirectivesFromOtherEntryPoints,
            reportAllDirectiveErrors: reportAllDirectiveErrors,
            directiveErrors: directiveErrors,
            otherEntryPoints: out otherEntryPoints,
            parsedFiles: out parsedFiles);
    }

    private static void DiscoverOtherFiles(
        SourceFile? entryPointFile,
        DirectoryInfo? entryDirectory,
        CacheInfo? cache,
        bool parseDirectivesFromOtherEntryPoints,
        bool reportAllDirectiveErrors,
        ImmutableArray<SimpleDiagnostic>.Builder? directiveErrors,
        out ImmutableArray<string> otherEntryPoints,
        out IReadOnlyDictionary<string, ParsedSourceFile> parsedFiles)
    {
        // Sorted by file path for deterministic results when building `sortedDirectives`.
        var parsedFilesBuilder = new SortedDictionary<string, (SourceFile File, bool IsEntryPoint, ImmutableArray<CSharpDirective> Directives)>(StringComparer.Ordinal);

        // Process entry-point file if provided.
        if (entryPointFile is { } entryPointFileValue)
        {
            if (!HasTopLevelStatements(entryPointFileValue))
            {
                throw new GracefulException(CliCommandStrings.NoTopLevelStatements, entryPointFileValue.Path);
            }

            // Parse directives in the entry-point file.
            var directives = FindDirectives(entryPointFileValue, reportAllErrors: reportAllDirectiveErrors, errors: directiveErrors);
            parsedFilesBuilder.Add(entryPointFileValue.Path, (entryPointFileValue, IsEntryPoint: true, directives));
        }

        // Discover other C# files.
        var otherEntryPointsBuilder = ImmutableArray.CreateBuilder<string>();
        var otherSources = cache?.OtherSources ?? FindOtherFiles(
            entryPointFile: entryPointFile?.GetFileInfo(),
            entryDirectory: entryDirectory)
            .Select(ToOtherSourceFile);
        foreach (var other in otherSources)
        {
            var file = other.GetOrLoadFile();

            if (other.IsEntryPoint)
            {
                if (!other.IsTopLevel)
                {
                    throw new GracefulException(CliCommandStrings.EntryPointInNestedFolder, file.Path);
                }

                otherEntryPointsBuilder.Add(file.Path);
                if (parseDirectivesFromOtherEntryPoints)
                {
                    var directives = FindDirectives(file, reportAllErrors: reportAllDirectiveErrors, errors: directiveErrors);
                    parsedFilesBuilder.Add(file.Path, (file, IsEntryPoint: true, directives));
                }
            }
            else
            {
                var directives = FindDirectives(file, reportAllErrors: reportAllDirectiveErrors, errors: directiveErrors);
                parsedFilesBuilder.Add(file.Path, (file, IsEntryPoint: false, directives));
            }
        }

        otherEntryPoints = otherEntryPointsBuilder.DrainToImmutable();

        var sharedDirectives = parsedFilesBuilder.Values.Where(f => !f.IsEntryPoint).SelectMany(f => f.Directives).ToList();
        parsedFiles = parsedFilesBuilder.Values.ToDictionary(
            keySelector: f => f.File.Path,
            elementSelector: ComputeParsedSourceFile,
            comparer: StringComparer.Ordinal);

        ParsedSourceFile ComputeParsedSourceFile((SourceFile File, bool IsEntryPoint, ImmutableArray<CSharpDirective> Directives) input)
        {
            return new ParsedSourceFile
            {
                File = input.File,
                Directives = input.Directives,
                SortedDirectives = input.IsEntryPoint ? [.. sharedDirectives, .. input.Directives] : default,
            };
        }
    }

    private static bool HasTopLevelStatements(SourceFile file)
    {
        var tree = (CSharpSyntaxTree)CSharpSyntaxTree.ParseText(file.Text, path: file.Path);
        return tree.GetRoot().ChildNodes().OfType<GlobalStatementSyntax>().Any();
    }

    /// <summary>
    /// Discovers inputs that can be used to create <see cref="OtherSourceFile"/>s (via <see cref="ToOtherSourceFile"/>).
    /// </summary>
    private static IEnumerable<(FileInfo File, bool IsTopLevel)> FindOtherFiles(FileInfo? entryPointFile, DirectoryInfo? entryDirectory)
    {
        entryDirectory ??= entryPointFile?.Directory;
        Debug.Assert(entryDirectory != null);
        var files = entryDirectory.EnumerateFiles("*.cs", s_csEnumerationOptions)
            .OrderBy(f => f.FullName, StringComparer.Ordinal);
        foreach (var file in files)
        {
            // Skip the current entry point (FileInfo.FullName is a normalized path, so we can compare it).
            if (entryPointFile?.FullName.Equals(file.FullName, StringComparison.OrdinalIgnoreCase) == true)
            {
                continue;
            }

            bool isTopLevel = entryDirectory.FullName.Equals(file.Directory?.FullName, StringComparison.Ordinal);

            yield return (File: file, IsTopLevel: isTopLevel);
        }
    }

    private static OtherSourceFile ToOtherSourceFile((FileInfo File, bool IsTopLevel) input)
    {
        SourceFile file = LoadSourceFile(input.File.FullName);

        bool isEntryPoint = HasTopLevelStatements(file);

        return new OtherSourceFile
        {
            Path = file.Path,
            Text = file.Text,
            IsEntryPoint = isEntryPoint,
            IsTopLevel = input.IsTopLevel,
        };
    }

    public ProjectInstance CreateProjectInstance(ProjectCollection projectCollection)
    {
        return CreateProjectInstance(projectCollection, addGlobalProperties: null);
    }

    private ProjectInstance CreateProjectInstance(
        ProjectCollection projectCollection,
        Action<IDictionary<string, string>>? addGlobalProperties)
    {
        var projectRoot = CreateProjectRootElement(projectCollection);

        var globalProperties = projectCollection.GlobalProperties;
        if (addGlobalProperties is not null)
        {
            globalProperties = new Dictionary<string, string>(projectCollection.GlobalProperties, StringComparer.OrdinalIgnoreCase);
            addGlobalProperties(globalProperties);
        }

        return ProjectInstance.FromProjectRootElement(projectRoot, new ProjectOptions
        {
            GlobalProperties = globalProperties,
        });

        ProjectRootElement CreateProjectRootElement(ProjectCollection projectCollection)
        {
            Debug.Assert(_projectFileText != null, $"{nameof(PrepareProjectInstance)} should have been called first.");

            using var reader = new StringReader(_projectFileText);
            using var xmlReader = XmlReader.Create(reader);
            var projectRoot = ProjectRootElement.Create(xmlReader, projectCollection);
            projectRoot.FullPath = Path.ChangeExtension(EntryPointFileFullPath, ".csproj");
            return projectRoot;
        }
    }

    private string GetArtifactsPath() => GetArtifactsPath(EntryPointFileFullPath);

    // internal for testing
    internal static string GetArtifactsPath(string entryPointFileFullPath)
    {
        // We want a location where permissions are expected to be restricted to the current user.
        string directory = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.GetTempPath()
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Include entry point file name so the directory name is not completely opaque.
        string fileName = Path.GetFileNameWithoutExtension(entryPointFileFullPath);
        string hash = Sha256Hasher.HashWithNormalizedCasing(entryPointFileFullPath);
        string directoryName = $"{fileName}-{hash}";

        return Path.Join(directory, "dotnet", "runfile", directoryName);
    }

    public static void WriteProjectFile(
        TextWriter writer,
        ImmutableArray<CSharpDirective> directives,
        ProjectWritingOptions options)
    {
        int processedDirectives = 0;

        var sdkDirectives = directives.OfType<CSharpDirective.Sdk>();
        var propertyDirectives = directives.OfType<CSharpDirective.Property>();
        var packageDirectives = directives.OfType<CSharpDirective.Package>();

        string sdkValue = "Microsoft.NET.Sdk";

        if (sdkDirectives.FirstOrDefault() is { } firstSdk)
        {
            sdkValue = firstSdk.ToSlashDelimitedString();
            processedDirectives++;
        }

        if (options is ProjectWritingOptions.Virtual { ArtifactsPath: var artifactsPath })
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(artifactsPath));

            writer.WriteLine($"""
                <Project>

                  <PropertyGroup>
                    <IncludeProjectNameInArtifactsPaths>false</IncludeProjectNameInArtifactsPaths>
                    <ArtifactsPath>{EscapeValue(artifactsPath)}</ArtifactsPath>
                  </PropertyGroup>

                  <!-- We need to explicitly import Sdk props/targets so we can override the targets below. -->
                  <Import Project="Sdk.props" Sdk="{EscapeValue(sdkValue)}" />
                """);
        }
        else
        {
            writer.WriteLine($"""
                <Project Sdk="{EscapeValue(sdkValue)}">

                """);
        }

        foreach (var sdk in sdkDirectives.Skip(1))
        {
            if (options is ProjectWritingOptions.Virtual)
            {
                writer.WriteLine($"""
                      <Import Project="Sdk.props" Sdk="{EscapeValue(sdk.ToSlashDelimitedString())}" />
                    """);
            }
            else if (sdk.Version is null)
            {
                writer.WriteLine($"""
                      <Sdk Name="{EscapeValue(sdk.Name)}" />
                    """);
            }
            else
            {
                writer.WriteLine($"""
                      <Sdk Name="{EscapeValue(sdk.Name)}" Version="{EscapeValue(sdk.Version)}" />
                    """);
            }

            processedDirectives++;
        }

        if (processedDirectives > 1 || options is ProjectWritingOptions.Virtual)
        {
            writer.WriteLine();
        }

        // Kept in sync with the default `dotnet new console` project file (enforced by `DotnetProjectAddTests.SameAsTemplate`).
        writer.WriteLine($"""
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            """);

        if (propertyDirectives.Any())
        {
            writer.WriteLine("""

                  <PropertyGroup>
                """);

            foreach (var property in propertyDirectives)
            {
                writer.WriteLine($"""
                        <{property.Name}>{EscapeValue(property.Value)}</{property.Name}>
                    """);

                processedDirectives++;
            }

            writer.WriteLine("  </PropertyGroup>");
        }

        if (options is ProjectWritingOptions.Virtual)
        {
            // After `#:property` directives so they don't override this.
            writer.WriteLine("""

                  <PropertyGroup>
                    <Features>$(Features);FileBasedProgram</Features>
                  </PropertyGroup>
                """);
        }

        if (packageDirectives.Any())
        {
            writer.WriteLine("""

                  <ItemGroup>
                """);

            foreach (var package in packageDirectives)
            {
                if (package.Version is null)
                {
                    writer.WriteLine($"""
                            <PackageReference Include="{EscapeValue(package.Name)}" />
                        """);
                }
                else
                {
                    writer.WriteLine($"""
                            <PackageReference Include="{EscapeValue(package.Name)}" Version="{EscapeValue(package.Version)}" />
                        """);
                }

                processedDirectives++;
            }

            writer.WriteLine("  </ItemGroup>");
        }

        Debug.Assert(processedDirectives + directives.OfType<CSharpDirective.Shebang>().Count() == directives.Length);

        if (options is ProjectWritingOptions.Virtual { ExcludeCompileItems: var excludeCompileItems })
        {
            if (!excludeCompileItems.IsEmpty)
            {
                writer.WriteLine("""

                      <ItemGroup>
                    """);

                foreach (var excludeCompileItem in excludeCompileItems)
                {
                    writer.WriteLine($"""
                            <Compile Remove="{EscapeValue(excludeCompileItem)}" />
                        """);
                }

                writer.WriteLine("  </ItemGroup>");
            }
        }
        else if (options is ProjectWritingOptions.Converted { SharedDirectoryName: string sharedDirectoryName })
        {
            // Currently we handle only C# files but we should handle more (like .resx).
            writer.WriteLine($"""

                      <ItemGroup>
                        <Compile Include="..\{sharedDirectoryName}\**\*.cs" />
                      </ItemGroup>
                    """);
        }

        if (options is ProjectWritingOptions.Virtual)
        {
            writer.WriteLine();

            foreach (var sdk in sdkDirectives)
            {
                writer.WriteLine($"""
                      <Import Project="Sdk.targets" Sdk="{EscapeValue(sdk.ToSlashDelimitedString())}" />
                    """);
            }

            if (!sdkDirectives.Any())
            {
                Debug.Assert(sdkValue == "Microsoft.NET.Sdk");
                writer.WriteLine("""
                      <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
                    """);
            }

            writer.WriteLine("""

                  <!--
                    Override targets which don't work with project files that are not present on disk.
                    See https://github.com/NuGet/Home/issues/14148.
                  -->

                  <Target Name="_FilterRestoreGraphProjectInputItems"
                          DependsOnTargets="_LoadRestoreGraphEntryPoints"
                          Returns="@(FilteredRestoreGraphProjectInputItems)">
                    <ItemGroup>
                      <FilteredRestoreGraphProjectInputItems Include="@(RestoreGraphProjectInputItems)" />
                    </ItemGroup>
                  </Target>

                  <Target Name="_GetAllRestoreProjectPathItems"
                          DependsOnTargets="_FilterRestoreGraphProjectInputItems"
                          Returns="@(_RestoreProjectPathItems)">
                    <ItemGroup>
                      <_RestoreProjectPathItems Include="@(FilteredRestoreGraphProjectInputItems)" />
                    </ItemGroup>
                  </Target>

                  <Target Name="_GenerateRestoreGraph"
                          DependsOnTargets="_FilterRestoreGraphProjectInputItems;_GetAllRestoreProjectPathItems;_GenerateRestoreGraphProjectEntry;_GenerateProjectRestoreGraph"
                          Returns="@(_RestoreGraphEntry)">
                    <!-- Output from dependency _GenerateRestoreGraphProjectEntry and _GenerateProjectRestoreGraph -->
                  </Target>
                """);
        }

        writer.WriteLine("""

            </Project>
            """);

        static string EscapeValue(string value) => SecurityElement.Escape(value);
    }

    /// <param name="reportAllErrors">
    /// If <see langword="true"/>, the whole <paramref name="sourceFile"/> is parsed to find diagnostics about every app directive.
    /// Otherwise, only directives up to the first C# token is checked.
    /// The former is useful for <c>dotnet project convert</c> where we want to report all errors because it would be difficult to fix them up after the conversion.
    /// The latter is useful for <c>dotnet run file.cs</c> where if there are app directives after the first token,
    /// compiler reports <see cref="ErrorCode.ERR_PPIgnoredFollowsToken"/> anyway, so we speed up success scenarios by not parsing the whole file up front in the SDK CLI.
    /// </param>
    /// <param name="errors">
    /// If <see langword="null"/>, the first error is thrown as <see cref="GracefulException"/>.
    /// Otherwise, all errors are put into the list.
    /// Does not have any effect when <paramref name="reportAllErrors"/> is <see langword="false"/>.
    /// </param>
    public static ImmutableArray<CSharpDirective> FindDirectives(SourceFile sourceFile, bool reportAllErrors, ImmutableArray<SimpleDiagnostic>.Builder? errors)
    {
#pragma warning disable RSEXPERIMENTAL003 // 'SyntaxTokenParser' is experimental

        var builder = ImmutableArray.CreateBuilder<CSharpDirective>();
        SyntaxTokenParser tokenizer = SyntaxFactory.CreateTokenParser(sourceFile.Text,
            CSharpParseOptions.Default.WithFeatures([new("FileBasedProgram", "true")]));

        var result = tokenizer.ParseLeadingTrivia();
        TextSpan previousWhiteSpaceSpan = default;
        foreach (var trivia in result.Token.LeadingTrivia)
        {
            // Stop when the trivia contains an error (e.g., because it's after #if).
            if (trivia.ContainsDiagnostics)
            {
                break;
            }

            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                Debug.Assert(previousWhiteSpaceSpan.IsEmpty);
                previousWhiteSpaceSpan = trivia.FullSpan;
                continue;
            }

            if (trivia.IsKind(SyntaxKind.ShebangDirectiveTrivia))
            {
                TextSpan span = getFullSpan(previousWhiteSpaceSpan, trivia);

                builder.Add(new CSharpDirective.Shebang { Span = span });
            }
            else if (trivia.IsKind(SyntaxKind.IgnoredDirectiveTrivia))
            {
                TextSpan span = getFullSpan(previousWhiteSpaceSpan, trivia);

                var message = trivia.GetStructure() is IgnoredDirectiveTriviaSyntax { Content: { RawKind: (int)SyntaxKind.StringLiteralToken } content }
                    ? content.Text.AsSpan().Trim()
                    : "";
                var parts = Patterns.Whitespace.EnumerateSplits(message, 2);
                var name = parts.MoveNext() ? message[parts.Current] : default;
                var value = parts.MoveNext() ? message[parts.Current] : default;
                Debug.Assert(!parts.MoveNext());

                if (CSharpDirective.Parse(errors, sourceFile, span, name.ToString(), value.ToString()) is { } directive)
                {
                    builder.Add(directive);
                }
            }

            previousWhiteSpaceSpan = default;
        }

        // In conversion mode, we want to report errors for any invalid directives in the rest of the file
        // so users don't end up with invalid directives in the converted project.
        if (reportAllErrors)
        {
            tokenizer.ResetTo(result);

            do
            {
                result = tokenizer.ParseNextToken();

                foreach (var trivia in result.Token.LeadingTrivia)
                {
                    reportErrorFor(trivia);
                }

                foreach (var trivia in result.Token.TrailingTrivia)
                {
                    reportErrorFor(trivia);
                }
            }
            while (!result.Token.IsKind(SyntaxKind.EndOfFileToken));
        }

        // The result should be ordered by source location, RemoveDirectivesFromFile depends on that.
        return builder.ToImmutable();

        static TextSpan getFullSpan(TextSpan previousWhiteSpaceSpan, SyntaxTrivia trivia)
        {
            // Include the preceding whitespace in the span, i.e., span will be the whole line.
            return previousWhiteSpaceSpan.IsEmpty ? trivia.FullSpan : TextSpan.FromBounds(previousWhiteSpaceSpan.Start, trivia.FullSpan.End);
        }

        void reportErrorFor(SyntaxTrivia trivia)
        {
            if (trivia.ContainsDiagnostics && trivia.IsKind(SyntaxKind.IgnoredDirectiveTrivia))
            {
                string location = sourceFile.GetLocationString(trivia.Span);
                if (errors != null)
                {
                    errors.Add(new SimpleDiagnostic
                    {
                        Location = sourceFile.GetFileLinePositionSpan(trivia.Span),
                        Message = string.Format(CliCommandStrings.CannotConvertDirective, location),
                    });
                }
                else
                {
                    throw new GracefulException(CliCommandStrings.CannotConvertDirective, location);
                }
            }
        }
#pragma warning restore RSEXPERIMENTAL003 // 'SyntaxTokenParser' is experimental
    }

    public static SourceFile LoadSourceFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return new SourceFile(filePath, SourceText.From(stream, Encoding.UTF8));
    }

    public static SourceText? RemoveDirectivesFromFile(ImmutableArray<CSharpDirective> directives, SourceText text)
    {
        if (directives.Length == 0)
        {
            return null;
        }

        Debug.Assert(directives.OrderBy(d => d.Span.Start).SequenceEqual(directives), "Directives should be ordered by source location.");

        for (int i = directives.Length - 1; i >= 0; i--)
        {
            var directive = directives[i];
            text = text.Replace(directive.Span, string.Empty);
        }

        return text;
    }

    public static bool IsValidEntryPointPath(string entryPointFilePath)
    {
        return entryPointFilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && File.Exists(entryPointFilePath);
    }
}

internal abstract class ProjectWritingOptions
{
    private ProjectWritingOptions() { }

    public sealed class Virtual : ProjectWritingOptions
    {
        public required string ArtifactsPath { get; init; }
        public required ImmutableArray<string> ExcludeCompileItems { get; init; }
    }

    public sealed class Converted : ProjectWritingOptions
    {
        public required string? SharedDirectoryName { get; init; }
    }
}

internal readonly record struct SourceFile(string Path, SourceText Text)
{
    public FileLinePositionSpan GetFileLinePositionSpan(TextSpan span)
    {
        return new FileLinePositionSpan(Path, Text.Lines.GetLinePositionSpan(span));
    }

    public string GetLocationString(TextSpan span)
    {
        var positionSpan = GetFileLinePositionSpan(span);
        return $"{positionSpan.Path}:{positionSpan.StartLinePosition.Line + 1}";
    }

    public FileInfo GetFileInfo() => new FileInfo(Path);
}

/// <summary>
/// C# source file that is in the directory subtree of the current entry point but is not the current entry point.
/// </summary>
internal readonly struct OtherSourceFile
{
    public required string Path { get; init; }

    public SourceText? Text { get; init; }

    /// <summary>
    /// <see langword="false"/> if the file is in a folder nested relative to the current entry point.
    /// </summary>
    public required bool IsTopLevel { get; init; }

    /// <summary>
    /// <see langword="true"/> if this is another entry point that should be excluded.
    /// </summary>
    public required bool IsEntryPoint { get; init; }

    public SourceFile GetOrLoadFile()
    {
        if (Text != null)
        {
            return new SourceFile(Path, Text);
        }

        return VirtualProjectBuildingCommand.LoadSourceFile(Path);
    }
}

internal readonly struct ParsedSourceFile
{
    public required SourceFile File { get; init; }

    public required ImmutableArray<CSharpDirective> Directives { get; init; }

    /// <summary>
    /// If this <see cref="ParsedSourceFile"/> represents an entry point,
    /// contains directives from all non-entry-point files and the current entry point,
    /// sorted in a deterministic order so they can be used to generate a project file.
    /// Otherwise, this is <see langword="default"/>.
    /// </summary>
    public required ImmutableArray<CSharpDirective> SortedDirectives { get; init; }

    public bool IsEntryPoint => !SortedDirectives.IsDefault;
}

internal static partial class Patterns
{
    [GeneratedRegex("""\s+""")]
    public static partial Regex Whitespace { get; }
}

/// <summary>
/// Represents a C# directive starting with <c>#:</c>. Those are ignored by the language but recognized by us.
/// </summary>
internal abstract class CSharpDirective
{
    private CSharpDirective() { }

    /// <summary>
    /// Span of the full line including the trailing line break.
    /// </summary>
    public required TextSpan Span { get; init; }

    public static CSharpDirective? Parse(ImmutableArray<SimpleDiagnostic>.Builder? errors, SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText)
    {
        return directiveKind switch
        {
            "sdk" => Sdk.Parse(errors, sourceFile, span, directiveKind, directiveText),
            "property" => Property.Parse(errors, sourceFile, span, directiveKind, directiveText),
            "package" => Package.Parse(errors, sourceFile, span, directiveKind, directiveText),
            _ => ReportError<CSharpDirective>(errors, sourceFile, span, string.Format(CliCommandStrings.UnrecognizedDirective, directiveKind, sourceFile.GetLocationString(span))),
        };
    }

    private static T? ReportError<T>(ImmutableArray<SimpleDiagnostic>.Builder? errors, SourceFile sourceFile, TextSpan span, string message, Exception? inner = null)
    {
        if (errors != null)
        {
            errors.Add(new SimpleDiagnostic { Location = sourceFile.GetFileLinePositionSpan(span), Message = message });
            return default;
        }
        else
        {
            throw new GracefulException(message, inner);
        }
    }

    private static (string, string?)? ParseOptionalTwoParts(ImmutableArray<SimpleDiagnostic>.Builder? errors, SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText, SearchValues<char>? separators = null)
    {
        var i = separators != null
            ? directiveText.AsSpan().IndexOfAny(separators)
            : directiveText.IndexOf(' ', StringComparison.Ordinal);
        var firstPart = i < 0 ? directiveText : directiveText[..i];

        if (string.IsNullOrWhiteSpace(firstPart))
        {
            return ReportError<(string, string?)?>(errors, sourceFile, span, string.Format(CliCommandStrings.MissingDirectiveName, directiveKind, sourceFile.GetLocationString(span)));
        }

        var secondPart = i < 0 ? [] : directiveText.AsSpan((i + 1)..).TrimStart();
        if (i < 0 || secondPart.IsWhiteSpace())
        {
            return (firstPart, null);
        }

        return (firstPart, secondPart.ToString());
    }

    /// <summary>
    /// <c>#!</c> directive.
    /// </summary>
    public sealed class Shebang : CSharpDirective;

    /// <summary>
    /// <c>#:sdk</c> directive.
    /// </summary>
    public sealed class Sdk : CSharpDirective
    {
        private Sdk() { }

        public required string Name { get; init; }
        public string? Version { get; init; }

        public static new Sdk? Parse(ImmutableArray<SimpleDiagnostic>.Builder? errors, SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText)
        {
            if (ParseOptionalTwoParts(errors, sourceFile, span, directiveKind, directiveText) is not var (sdkName, sdkVersion))
            {
                return null;
            }

            return new Sdk
            {
                Span = span,
                Name = sdkName,
                Version = sdkVersion,
            };
        }

        public string ToSlashDelimitedString()
        {
            return Version is null ? Name : $"{Name}/{Version}";
        }
    }

    /// <summary>
    /// <c>#:property</c> directive.
    /// </summary>
    public sealed class Property : CSharpDirective
    {
        private Property() { }

        public required string Name { get; init; }
        public required string Value { get; init; }

        public static new Property? Parse(ImmutableArray<SimpleDiagnostic>.Builder? errors, SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText)
        {
            if (ParseOptionalTwoParts(errors, sourceFile, span, directiveKind, directiveText) is not var (propertyName, propertyValue))
            {
                return null;
            }

            if (propertyValue is null)
            {
                return ReportError<Property?>(errors, sourceFile, span, string.Format(CliCommandStrings.PropertyDirectiveMissingParts, sourceFile.GetLocationString(span)));
            }

            try
            {
                propertyName = XmlConvert.VerifyName(propertyName);
            }
            catch (XmlException ex)
            {
                return ReportError<Property?>(errors, sourceFile, span, string.Format(CliCommandStrings.PropertyDirectiveInvalidName, sourceFile.GetLocationString(span), ex.Message), ex);
            }

            return new Property
            {
                Span = span,
                Name = propertyName,
                Value = propertyValue,
            };
        }
    }

    /// <summary>
    /// <c>#:package</c> directive.
    /// </summary>
    public sealed class Package : CSharpDirective
    {
        private static readonly SearchValues<char> s_separators = SearchValues.Create(' ', '@');

        private Package() { }

        public required string Name { get; init; }
        public string? Version { get; init; }

        public static new Package? Parse(ImmutableArray<SimpleDiagnostic>.Builder? errors, SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText)
        {
            if (ParseOptionalTwoParts(errors, sourceFile, span, directiveKind, directiveText, s_separators) is not var (packageName, packageVersion))
            {
                return null;
            }

            return new Package
            {
                Span = span,
                Name = packageName,
                Version = packageVersion,
            };
        }
    }
}

internal sealed class SimpleDiagnostic
{
    public required Position Location { get; init; }
    public required string Message { get; init; }

    /// <summary>
    /// An adapter of <see cref="FileLinePositionSpan"/> that ensures we JSON-serialize only the necessary fields.
    /// </summary>
    public readonly struct Position
    {
        public string Path { get; init; }
        public LinePositionSpan Span { get; init; }

        public static implicit operator Position(FileLinePositionSpan fileLinePositionSpan) => new()
        {
            Path = fileLinePositionSpan.Path,
            Span = fileLinePositionSpan.Span,
        };
    }
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
    /// Keys are full paths.
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, OtherSource> OtherSources { get; }

    public string? ProjectFileText { get; set; }

    [JsonConstructor]
    public RunFileBuildCacheEntry()
    {
        GlobalProperties = new(GlobalPropertiesComparer);
        ImplicitBuildFiles = new(FilePathComparer);
        OtherSources = new(FilePathComparer);
    }

    public RunFileBuildCacheEntry(Dictionary<string, string> globalProperties)
    {
        Debug.Assert(globalProperties.Comparer == GlobalPropertiesComparer);
        GlobalProperties = globalProperties;
        ImplicitBuildFiles = new(FilePathComparer);
        OtherSources = new(FilePathComparer);
    }

    public readonly record struct OtherSource(bool IsEntryPoint);
}

[JsonSerializable(typeof(RunFileBuildCacheEntry))]
internal partial class RunFileJsonSerializerContext : JsonSerializerContext;
