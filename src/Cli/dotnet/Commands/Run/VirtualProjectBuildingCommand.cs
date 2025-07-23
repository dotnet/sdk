﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
using Microsoft.DotNet.Cli.Commands.Clean.FileBasedAppArtifacts;
using Microsoft.DotNet.Cli.Commands.Restore;
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

    /// <remarks>
    /// Kept in sync with the default <c>dotnet new console</c> project file (enforced by <c>DotnetProjectAddTests.SameAsTemplate</c>).
    /// </remarks>
    private static readonly FrozenDictionary<string, string> s_defaultProperties = FrozenDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase,
    [
        new("OutputType", "Exe"),
        new("TargetFramework", "net10.0"),
        new("ImplicitUsings", "enable"),
        new("Nullable", "enable"),
        new("PublishAot", "true"),
    ]);

    internal static readonly string TargetOverrides = """
          <!--
            Override targets which don't work with project files that are not present on disk.
            See https://github.com/NuGet/Home/issues/14148.
          -->

          <Target Name="_FilterRestoreGraphProjectInputItems"
                  DependsOnTargets="_LoadRestoreGraphEntryPoints">
            <!-- No-op, the original output is not needed by the overwritten targets. -->
          </Target>

          <Target Name="_GetAllRestoreProjectPathItems"
                  DependsOnTargets="_FilterRestoreGraphProjectInputItems;_GenerateRestoreProjectPathWalk"
                  Returns="@(_RestoreProjectPathItems)">
            <!-- Output from dependency _GenerateRestoreProjectPathWalk. -->
          </Target>

          <Target Name="_GenerateRestoreGraph"
                  DependsOnTargets="_FilterRestoreGraphProjectInputItems;_GetAllRestoreProjectPathItems;_GenerateRestoreGraphProjectEntry;_GenerateProjectRestoreGraph"
                  Returns="@(_RestoreGraphEntry)">
            <!-- Output partly from dependency _GenerateRestoreGraphProjectEntry and _GenerateProjectRestoreGraph. -->

            <ItemGroup>
              <_GenerateRestoreGraphProjectEntryInput Include="@(_RestoreProjectPathItems)" Exclude="$(MSBuildProjectFullPath)" />
            </ItemGroup>

            <MSBuild
                BuildInParallel="$(RestoreBuildInParallel)"
                Projects="@(_GenerateRestoreGraphProjectEntryInput)"
                Targets="_GenerateRestoreGraphProjectEntry"
                Properties="$(_GenerateRestoreGraphProjectEntryInputProperties)">

              <Output
                  TaskParameter="TargetOutputs"
                  ItemName="_RestoreGraphEntry" />
            </MSBuild>

            <MSBuild
                BuildInParallel="$(RestoreBuildInParallel)"
                Projects="@(_GenerateRestoreGraphProjectEntryInput)"
                Targets="_GenerateProjectRestoreGraph"
                Properties="$(_GenerateRestoreGraphProjectEntryInputProperties)">

              <Output
                  TaskParameter="TargetOutputs"
                  ItemName="_RestoreGraphEntry" />
            </MSBuild>
          </Target>
        """;

    public VirtualProjectBuildingCommand(
        string entryPointFileFullPath,
        MSBuildArgs msbuildArgs)
    {
        Debug.Assert(Path.IsPathFullyQualified(entryPointFileFullPath));

        EntryPointFileFullPath = entryPointFileFullPath;
        MSBuildArgs = msbuildArgs;
    }

    public string EntryPointFileFullPath { get; }
    public MSBuildArgs MSBuildArgs { get; }
    public string? CustomArtifactsPath { get; init; }
    public bool NoRestore { get; init; }

    /// <summary>
    /// If <see langword="true"/>, build markers are not checked and hence MSBuild is always run.
    /// This property does not control whether the build markers are written, use <see cref="NoWriteBuildMarkers"/> for that.
    /// </summary>
    public bool NoCache { get; init; }

    public bool NoBuild { get; init; }

    /// <summary>
    /// If <see langword="true"/>, no build markers are written
    /// (like <see cref="BuildStartCacheFileName"/> and <see cref="BuildSuccessCacheFileName"/>).
    /// Also skips automatic cleanup.
    /// This property does not control whether the markers are checked, use <see cref="NoCache"/> for that.
    /// </summary>
    public bool NoWriteBuildMarkers { get; init; }

    public ImmutableArray<CSharpDirective> Directives
    {
        get
        {
            if (field.IsDefault)
            {
                var sourceFile = SourceFile.Load(EntryPointFileFullPath);
                field = FindDirectives(sourceFile, reportAllErrors: false, DiagnosticBag.ThrowOnFirst());
                Debug.Assert(!field.IsDefault);
            }

            return field;
        }

        set;
    }

    public override int Execute()
    {
        Debug.Assert(!(NoRestore && NoBuild));
        var verbosity = MSBuildArgs.Verbosity ?? VerbosityOptions.quiet;
        var consoleLogger = TerminalLogger.CreateTerminalOrConsoleLogger([$"--verbosity:{verbosity}", .. MSBuildArgs.OtherMSBuildArgs]);
        var binaryLogger = GetBinaryLogger(MSBuildArgs.OtherMSBuildArgs);

        RunFileBuildCacheEntry? cacheEntry = null;

        if (!NoBuild)
        {
            if (NoCache)
            {
                cacheEntry = ComputeCacheEntry(out _);
            }
            else if (!NeedsToBuild(out cacheEntry))
            {
                if (binaryLogger is not null)
                {
                    Reporter.Output.WriteLine(CliCommandStrings.NoBinaryLogBecauseUpToDate.Yellow());
                }

                MarkArtifactsFolderUsed();
                return 0;
            }

            MarkBuildStart();
        }
        else if (!NoWriteBuildMarkers)
        {
            CreateTempSubdirectory(GetArtifactsPath());
            MarkArtifactsFolderUsed();
        }

        if (!NoWriteBuildMarkers)
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

            // Do a restore first (equivalent to MSBuild's "implicit restore", i.e., `/restore`).
            // See https://github.com/dotnet/msbuild/blob/a1c2e7402ef0abe36bf493e395b04dd2cb1b3540/src/MSBuild/XMake.cs#L1838
            // and https://github.com/dotnet/msbuild/issues/11519.
            if (!NoRestore)
            {
                var restoreRequest = new BuildRequestData(
                    CreateProjectInstance(projectCollection, addGlobalProperties: AddRestoreGlobalProperties(MSBuildArgs.RestoreGlobalProperties)),
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
                    targetsToBuild: MSBuildArgs.RequestedTargets ?? ["Build"]);

                var buildResult = BuildManager.DefaultBuildManager.BuildRequest(buildRequest);
                if (buildResult.OverallResult != BuildResultCode.Success)
                {
                    return 1;
                }

                Debug.Assert(cacheEntry != null);
                MarkBuildSuccess(cacheEntry);
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

            binaryLogger?.Value.ReallyShutdown();
            consoleLogger.Shutdown();
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
    }

    /// <summary>
    /// Compute current cache entry - we need to do this always:
    /// <list type="bullet">
    /// <item>if we can skip build, we still need to check everything in the cache entry (e.g., implicit build files)</item>
    /// <item>if we have to build, we need to have the cache entry to write it to the success cache file</item>
    /// </list>
    /// </summary>
    private RunFileBuildCacheEntry ComputeCacheEntry(out FileInfo entryPointFileInfo)
    {
        var cacheEntry = new RunFileBuildCacheEntry(MSBuildArgs.GlobalProperties?.ToDictionary(StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        entryPointFileInfo = new FileInfo(EntryPointFileFullPath);

        // Collect current implicit build files.
        DirectoryInfo? directory = entryPointFileInfo.Directory;
        while (directory != null)
        {
            foreach (var implicitBuildFileName in s_implicitBuildFileNames)
            {
                string implicitBuildFilePath = Path.Join(directory.FullName, implicitBuildFileName);
                var implicitBuildFileInfo = new FileInfo(implicitBuildFilePath);
                if (implicitBuildFileInfo.Exists)
                {
                    cacheEntry.ImplicitBuildFiles.Add(implicitBuildFilePath, implicitBuildFileInfo.LastWriteTimeUtc);
                }
            }

            directory = directory.Parent;
        }

        return cacheEntry;
    }

    private bool NeedsToBuild(out RunFileBuildCacheEntry cacheEntry)
    {
        cacheEntry = ComputeCacheEntry(out FileInfo entryPointFileInfo);

        // Check cache files.

        string artifactsDirectory = GetArtifactsPath();
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

        if (startCacheFile.LastWriteTimeUtc > successCacheFile.LastWriteTimeUtc)
        {
            Reporter.Verbose.WriteLine("Building because start cache file is newer than success cache file (previous build likely failed): " + startCacheFile.FullName);
            return true;
        }

        var previousCacheEntry = DeserializeCacheEntry(successCacheFile);
        if (previousCacheEntry is null)
        {
            Reporter.Verbose.WriteLine("Building because previous cache entry could not be deserialized: " + successCacheFile.FullName);
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

        DateTime buildTimeUtc = successCacheFile.LastWriteTimeUtc;

        // Check that the source file is up to date.
        // If it does not exist, we also want to build.
        if (!entryPointFileInfo.Exists || entryPointFileInfo.LastWriteTimeUtc > buildTimeUtc)
        {
            Reporter.Verbose.WriteLine("Building because entry point file is missing or modified: " + entryPointFileInfo.FullName);
            return true;
        }

        // Check that implicit build files are up to date.
        foreach (var implicitBuildFilePath in previousCacheEntry.ImplicitBuildFiles.Keys)
        {
            var implicitBuildFileInfo = new FileInfo(implicitBuildFilePath);
            if (!implicitBuildFileInfo.Exists || implicitBuildFileInfo.LastWriteTimeUtc > buildTimeUtc)
            {
                Reporter.Verbose.WriteLine("Building because implicit build file is missing or modified: " + implicitBuildFileInfo.FullName);
                return true;
            }
        }

        // Check that no new implicit build files are present.
        foreach (var implicitBuildFilePath in cacheEntry.ImplicitBuildFiles.Keys)
        {
            if (!previousCacheEntry.ImplicitBuildFiles.ContainsKey(implicitBuildFilePath))
            {
                Reporter.Verbose.WriteLine("Building because new implicit build file is present: " + implicitBuildFilePath);
                return true;
            }
        }

        return false;

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

    /// <summary>
    /// Touching the artifacts folder ensures it's considered as recently used and not cleaned up by <see cref="CleanFileBasedAppArtifactsCommand"/>.
    /// </summary>
    public void MarkArtifactsFolderUsed()
    {
        if (NoWriteBuildMarkers)
        {
            return;
        }

        string directory = GetArtifactsPath();

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

        string directory = GetArtifactsPath();

        CreateTempSubdirectory(directory);

        MarkArtifactsFolderUsed();

        File.WriteAllText(Path.Join(directory, BuildStartCacheFileName), EntryPointFileFullPath);
    }

    private void MarkBuildSuccess(RunFileBuildCacheEntry cacheEntry)
    {
        if (NoWriteBuildMarkers)
        {
            return;
        }

        string successCacheFile = Path.Join(GetArtifactsPath(), BuildSuccessCacheFileName);
        using var stream = File.Open(successCacheFile, FileMode.Create, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, cacheEntry, RunFileJsonSerializerContext.Default.RunFileBuildCacheEntry);
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
            ProjectCollection = projectCollection,
            GlobalProperties = globalProperties,
        });

        ProjectRootElement CreateProjectRootElement(ProjectCollection projectCollection)
        {
            var projectFileFullPath = Path.ChangeExtension(EntryPointFileFullPath, ".csproj");
            var projectFileWriter = new StringWriter();
            WriteProjectFile(
                projectFileWriter,
                Directives,
                isVirtualProject: true,
                targetFilePath: EntryPointFileFullPath,
                artifactsPath: GetArtifactsPath(),
                includeRuntimeConfigInformation: !MSBuildArgs.RequestedTargets?.Contains("Publish") ?? true);
            var projectFileText = projectFileWriter.ToString();

            using var reader = new StringReader(projectFileText);
            using var xmlReader = XmlReader.Create(reader);
            var projectRoot = ProjectRootElement.Create(xmlReader, projectCollection);
            projectRoot.FullPath = projectFileFullPath;
            return projectRoot;
        }
    }

    private string GetArtifactsPath() => CustomArtifactsPath ?? GetArtifactsPath(EntryPointFileFullPath);

    public static string GetArtifactsPath(string entryPointFileFullPath)
    {
        // Include entry point file name so the directory name is not completely opaque.
        string fileName = Path.GetFileNameWithoutExtension(entryPointFileFullPath);
        string hash = Sha256Hasher.HashWithNormalizedCasing(entryPointFileFullPath);
        string directoryName = $"{fileName}-{hash}";

        return GetTempSubpath(directoryName);
    }

    /// <summary>
    /// Obtains a temporary subdirectory for file-based app artifacts, e.g., <c>/tmp/dotnet/runfile/</c>.
    /// </summary>
    public static string GetTempSubdirectory()
    {
        // We want a location where permissions are expected to be restricted to the current user.
        string directory = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.GetTempPath()
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return Path.Join(directory, "dotnet", "runfile");
    }

    /// <summary>
    /// Obtains a specific temporary path in a subdirectory for file-based app artifacts, e.g., <c>/tmp/dotnet/runfile/{name}</c>.
    /// </summary>
    public static string GetTempSubpath(string name)
    {
        return Path.Join(GetTempSubdirectory(), name);
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

    public static void WriteProjectFile(
        TextWriter writer,
        ImmutableArray<CSharpDirective> directives,
        bool isVirtualProject,
        string? targetFilePath = null,
        string? artifactsPath = null,
        bool includeRuntimeConfigInformation = true)
    {
        int processedDirectives = 0;

        var sdkDirectives = directives.OfType<CSharpDirective.Sdk>();
        var propertyDirectives = directives.OfType<CSharpDirective.Property>();
        var packageDirectives = directives.OfType<CSharpDirective.Package>();
        var projectDirectives = directives.OfType<CSharpDirective.Project>();

        string firstSdkName;
        string? firstSdkVersion;

        if (sdkDirectives.FirstOrDefault() is { } firstSdk)
        {
            firstSdkName = firstSdk.Name;
            firstSdkVersion = firstSdk.Version;
            processedDirectives++;
        }
        else
        {
            firstSdkName = "Microsoft.NET.Sdk";
            firstSdkVersion = null;
        }

        if (isVirtualProject)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(artifactsPath));

            // Note that ArtifactsPath needs to be specified before Sdk.props
            // (usually it's recommended to specify it in Directory.Build.props
            // but importing Sdk.props manually afterwards also works).
            writer.WriteLine($"""
                <Project>

                  <PropertyGroup>
                    <IncludeProjectNameInArtifactsPaths>false</IncludeProjectNameInArtifactsPaths>
                    <ArtifactsPath>{EscapeValue(artifactsPath)}</ArtifactsPath>
                    <PublishDir>artifacts/$(MSBuildProjectName)</PublishDir>
                    <FileBasedProgram>true</FileBasedProgram>
                  </PropertyGroup>

                  <ItemGroup>
                    <Clean Include="{EscapeValue(artifactsPath)}/*" />
                  </ItemGroup>

                  <!-- We need to explicitly import Sdk props/targets so we can override the targets below. -->
                """);

            if (firstSdkVersion is null)
            {
                writer.WriteLine($"""
                      <Import Project="Sdk.props" Sdk="{EscapeValue(firstSdkName)}" />
                    """);
            }
            else
            {
                writer.WriteLine($"""
                      <Import Project="Sdk.props" Sdk="{EscapeValue(firstSdkName)}" Version="{EscapeValue(firstSdkVersion)}" />
                    """);
            }
        }
        else
        {
            string slashDelimited = firstSdkVersion is null
                ? firstSdkName
                : $"{firstSdkName}/{firstSdkVersion}";
            writer.WriteLine($"""
                <Project Sdk="{EscapeValue(slashDelimited)}">

                """);
        }

        foreach (var sdk in sdkDirectives.Skip(1))
        {
            if (isVirtualProject)
            {
                WriteImport(writer, "Sdk.props", sdk);
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

        if (isVirtualProject || processedDirectives > 1)
        {
            writer.WriteLine();
        }

        // Write default and custom properties.
        {
            writer.WriteLine("""
                  <PropertyGroup>
                """);

            // First write the default properties except those specified by the user.
            var customPropertyNames = propertyDirectives.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, value) in s_defaultProperties)
            {
                if (!customPropertyNames.Contains(name))
                {
                    writer.WriteLine($"""
                            <{name}>{EscapeValue(value)}</{name}>
                        """);
                }
            }

            // Write virtual-only properties.
            if (isVirtualProject)
            {
                writer.WriteLine("""
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                    """);
            }

            // Write custom properties.
            foreach (var property in propertyDirectives)
            {
                writer.WriteLine($"""
                        <{property.Name}>{EscapeValue(property.Value)}</{property.Name}>
                    """);

                processedDirectives++;
            }

            // Write virtual-only properties which cannot be overridden.
            if (isVirtualProject)
            {
                writer.WriteLine("""
                        <Features>$(Features);FileBasedProgram</Features>
                    """);
            }

            writer.WriteLine("""
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

            writer.WriteLine("""
                  </ItemGroup>

                """);
        }

        if (projectDirectives.Any())
        {
            writer.WriteLine("""
                  <ItemGroup>
                """);

            foreach (var projectReference in projectDirectives)
            {
                writer.WriteLine($"""
                        <ProjectReference Include="{EscapeValue(projectReference.Name)}" />
                    """);

                processedDirectives++;
            }

            writer.WriteLine("""
                  </ItemGroup>

                """);
        }

        Debug.Assert(processedDirectives + directives.OfType<CSharpDirective.Shebang>().Count() == directives.Length);

        if (isVirtualProject)
        {
            Debug.Assert(targetFilePath is not null);

            writer.WriteLine($"""
                  <ItemGroup>
                    <Compile Include="{EscapeValue(targetFilePath)}" />
                  </ItemGroup>

                """);

            if (includeRuntimeConfigInformation)
            {
                var targetDirectory = Path.GetDirectoryName(targetFilePath) ?? "";
                writer.WriteLine($"""
                      <ItemGroup>
                        <RuntimeHostConfigurationOption Include="EntryPointFilePath" Value="{EscapeValue(targetFilePath)}" />
                        <RuntimeHostConfigurationOption Include="EntryPointFileDirectoryPath" Value="{EscapeValue(targetDirectory)}" />
                      </ItemGroup>

                    """);
            }

            foreach (var sdk in sdkDirectives)
            {
                WriteImport(writer, "Sdk.targets", sdk);
            }

            if (!sdkDirectives.Any())
            {
                Debug.Assert(firstSdkName == "Microsoft.NET.Sdk" && firstSdkVersion == null);
                writer.WriteLine("""
                      <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
                    """);
            }

            writer.WriteLine($"""

                {TargetOverrides}

                """);
        }

        writer.WriteLine("""
            </Project>
            """);

        static string EscapeValue(string value) => SecurityElement.Escape(value);

        static void WriteImport(TextWriter writer, string project, CSharpDirective.Sdk sdk)
        {
            if (sdk.Version is null)
            {
                writer.WriteLine($"""
                      <Import Project="{EscapeValue(project)}" Sdk="{EscapeValue(sdk.Name)}" />
                    """);
            }
            else
            {
                writer.WriteLine($"""
                      <Import Project="{EscapeValue(project)}" Sdk="{EscapeValue(sdk.Name)}" Version="{EscapeValue(sdk.Version)}" />
                    """);
            }
        }
    }

#pragma warning disable RSEXPERIMENTAL003 // 'SyntaxTokenParser' is experimental
    public static SyntaxTokenParser CreateTokenizer(SourceText text)
    {
        return SyntaxFactory.CreateTokenParser(text,
            CSharpParseOptions.Default.WithFeatures([new("FileBasedProgram", "true")]));
    }
#pragma warning restore RSEXPERIMENTAL003 // 'SyntaxTokenParser' is experimental

    /// <param name="reportAllErrors">
    /// If <see langword="true"/>, the whole <paramref name="sourceFile"/> is parsed to find diagnostics about every app directive.
    /// Otherwise, only directives up to the first C# token is checked.
    /// The former is useful for <c>dotnet project convert</c> where we want to report all errors because it would be difficult to fix them up after the conversion.
    /// The latter is useful for <c>dotnet run file.cs</c> where if there are app directives after the first token,
    /// compiler reports <see cref="ErrorCode.ERR_PPIgnoredFollowsToken"/> anyway, so we speed up success scenarios by not parsing the whole file up front in the SDK CLI.
    /// </param>
    public static ImmutableArray<CSharpDirective> FindDirectives(SourceFile sourceFile, bool reportAllErrors, DiagnosticBag diagnostics)
    {
        var deduplicated = new HashSet<CSharpDirective.Named>(NamedDirectiveComparer.Instance);
        var builder = ImmutableArray.CreateBuilder<CSharpDirective>();
        var tokenizer = CreateTokenizer(sourceFile.Text);

        var result = tokenizer.ParseLeadingTrivia();
        TextSpan previousWhiteSpaceSpan = default;
        var triviaList = result.Token.LeadingTrivia;
        foreach (var (index, trivia) in triviaList.Index())
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
                TextSpan span = GetFullSpan(previousWhiteSpaceSpan, trivia);

                var whiteSpace = GetWhiteSpaceInfo(triviaList, index);
                var info = new CSharpDirective.ParseInfo
                {
                    Span = span,
                    LeadingWhiteSpace = whiteSpace.Leading,
                    TrailingWhiteSpace = whiteSpace.Trailing,
                };
                builder.Add(new CSharpDirective.Shebang(info));
            }
            else if (trivia.IsKind(SyntaxKind.IgnoredDirectiveTrivia))
            {
                TextSpan span = GetFullSpan(previousWhiteSpaceSpan, trivia);

                var message = trivia.GetStructure() is IgnoredDirectiveTriviaSyntax { Content: { RawKind: (int)SyntaxKind.StringLiteralToken } content }
                    ? content.Text.AsSpan().Trim()
                    : "";
                var parts = Patterns.Whitespace.EnumerateSplits(message, 2);
                var name = parts.MoveNext() ? message[parts.Current] : default;
                var value = parts.MoveNext() ? message[parts.Current] : default;
                Debug.Assert(!parts.MoveNext());

                var whiteSpace = GetWhiteSpaceInfo(triviaList, index);
                var context = new CSharpDirective.ParseContext
                {
                    Info = new()
                    {
                        Span = span,
                        LeadingWhiteSpace = whiteSpace.Leading,
                        TrailingWhiteSpace = whiteSpace.Trailing,
                    },
                    Diagnostics = diagnostics,
                    SourceFile = sourceFile,
                    DirectiveKind = name.ToString(),
                    DirectiveText = value.ToString()
                };
                if (CSharpDirective.Parse(context) is { } directive)
                {
                    // If the directive is already present, report an error.
                    if (deduplicated.TryGetValue(directive, out var existingDirective))
                    {
                        var typeAndName = $"#:{existingDirective.GetType().Name.ToLowerInvariant()} {existingDirective.Name}";
                        diagnostics.AddError(sourceFile, directive.Info.Span, location => string.Format(CliCommandStrings.DuplicateDirective, typeAndName, location));
                    }
                    else
                    {
                        deduplicated.Add(directive);
                    }

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
                    ReportErrorFor(trivia);
                }

                foreach (var trivia in result.Token.TrailingTrivia)
                {
                    ReportErrorFor(trivia);
                }
            }
            while (!result.Token.IsKind(SyntaxKind.EndOfFileToken));
        }

        // The result should be ordered by source location, RemoveDirectivesFromFile depends on that.
        return builder.ToImmutable();

        static TextSpan GetFullSpan(TextSpan previousWhiteSpaceSpan, SyntaxTrivia trivia)
        {
            // Include the preceding whitespace in the span, i.e., span will be the whole line.
            return previousWhiteSpaceSpan.IsEmpty ? trivia.FullSpan : TextSpan.FromBounds(previousWhiteSpaceSpan.Start, trivia.FullSpan.End);
        }

        void ReportErrorFor(SyntaxTrivia trivia)
        {
            if (trivia.ContainsDiagnostics && trivia.IsKind(SyntaxKind.IgnoredDirectiveTrivia))
            {
                diagnostics.AddError(sourceFile, trivia.Span, location => string.Format(CliCommandStrings.CannotConvertDirective, location));
            }
        }

        static (WhiteSpaceInfo Leading, WhiteSpaceInfo Trailing) GetWhiteSpaceInfo(in SyntaxTriviaList triviaList, int index)
        {
            (WhiteSpaceInfo Leading, WhiteSpaceInfo Trailing) result = default;

            for (int i = index - 1; i >= 0; i--)
            {
                if (!Fill(ref result.Leading, triviaList, i)) break;
            }

            for (int i = index + 1; i < triviaList.Count; i++)
            {
                if (!Fill(ref result.Trailing, triviaList, i)) break;
            }

            return result;

            static bool Fill(ref WhiteSpaceInfo info, in SyntaxTriviaList triviaList, int index)
            {
                var trivia = triviaList[index];
                if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    info.LineBreaks += 1;
                    info.TotalLength += trivia.FullSpan.Length;
                    return true;
                }

                if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                {
                    info.TotalLength += trivia.FullSpan.Length;
                    return true;
                }

                return false;
            }
        }
    }

    public static SourceText? RemoveDirectivesFromFile(ImmutableArray<CSharpDirective> directives, SourceText text)
    {
        if (directives.Length == 0)
        {
            return null;
        }

        Debug.Assert(directives.OrderBy(d => d.Info.Span.Start).SequenceEqual(directives), "Directives should be ordered by source location.");

        for (int i = directives.Length - 1; i >= 0; i--)
        {
            var directive = directives[i];
            text = text.Replace(directive.Info.Span, string.Empty);
        }

        return text;
    }

    public static void RemoveDirectivesFromFile(ImmutableArray<CSharpDirective> directives, SourceText text, string filePath)
    {
        if (RemoveDirectivesFromFile(directives, text) is { } modifiedText)
        {
            new SourceFile(filePath, modifiedText).Save();
        }
    }

    public static bool IsValidEntryPointPath(string entryPointFilePath)
    {
        if (!File.Exists(entryPointFilePath))
        {
            return false;
        }

        if (entryPointFilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check if the first two characters are #!
        try
        {
            using var stream = File.OpenRead(entryPointFilePath);
            int first = stream.ReadByte();
            int second = stream.ReadByte();
            return first == '#' && second == '!';
        }
        catch
        {
            return false;
        }
    }
}

internal readonly record struct SourceFile(string Path, SourceText Text)
{
    public static SourceFile Load(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return new SourceFile(filePath, SourceText.From(stream, Encoding.UTF8));
    }

    public SourceFile WithText(SourceText newText)
    {
        return new SourceFile(Path, newText);
    }

    public void Save()
    {
        using var stream = File.Open(Path, FileMode.Create, FileAccess.Write);
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        Text.Write(writer);
    }

    public FileLinePositionSpan GetFileLinePositionSpan(TextSpan span)
    {
        return new FileLinePositionSpan(Path, Text.Lines.GetLinePositionSpan(span));
    }

    public string GetLocationString(TextSpan span)
    {
        var positionSpan = GetFileLinePositionSpan(span);
        return $"{positionSpan.Path}:{positionSpan.StartLinePosition.Line + 1}";
    }
}

internal static partial class Patterns
{
    [GeneratedRegex("""\s+""")]
    public static partial Regex Whitespace { get; }

    [GeneratedRegex("""[\s@=/]""")]
    public static partial Regex DisallowedNameCharacters { get; }
}

internal struct WhiteSpaceInfo
{
    public int LineBreaks;
    public int TotalLength;
}

/// <summary>
/// Represents a C# directive starting with <c>#:</c> (a.k.a., "file-level directive").
/// Those are ignored by the language but recognized by us.
/// </summary>
internal abstract class CSharpDirective(in CSharpDirective.ParseInfo info)
{
    public ParseInfo Info { get; } = info;

    public readonly struct ParseInfo
    {
        /// <summary>
        /// Span of the full line including the trailing line break.
        /// </summary>
        public required TextSpan Span { get; init; }
        public required WhiteSpaceInfo LeadingWhiteSpace { get; init; }
        public required WhiteSpaceInfo TrailingWhiteSpace { get; init; }
    }

    public readonly struct ParseContext
    {
        public required ParseInfo Info { get; init; }
        public required DiagnosticBag Diagnostics { get; init; }
        public required SourceFile SourceFile { get; init; }
        public required string DirectiveKind { get; init; }
        public required string DirectiveText { get; init; }
    }

    public static Named? Parse(in ParseContext context)
    {
        return context.DirectiveKind switch
        {
            "sdk" => Sdk.Parse(context),
            "property" => Property.Parse(context),
            "package" => Package.Parse(context),
            "project" => Project.Parse(context),
            var other => context.Diagnostics.AddError<Named>(context.SourceFile, context.Info.Span, location => string.Format(CliCommandStrings.UnrecognizedDirective, other, location)),
        };
    }

    private static (string, string?)? ParseOptionalTwoParts(in ParseContext context, char separator)
    {
        var i = context.DirectiveText.IndexOf(separator, StringComparison.Ordinal);
        var firstPart = (i < 0 ? context.DirectiveText : context.DirectiveText.AsSpan(..i)).TrimEnd();

        string directiveKind = context.DirectiveKind;
        if (firstPart.IsWhiteSpace())
        {
            return context.Diagnostics.AddError<(string, string?)?>(context.SourceFile, context.Info.Span, location => string.Format(CliCommandStrings.MissingDirectiveName, directiveKind, location));
        }

        // If the name contains characters that resemble separators, report an error to avoid any confusion.
        if (Patterns.DisallowedNameCharacters.IsMatch(firstPart))
        {
            return context.Diagnostics.AddError<(string, string?)?>(context.SourceFile, context.Info.Span, location => string.Format(CliCommandStrings.InvalidDirectiveName, directiveKind, separator, location));
        }

        var secondPart = i < 0 ? [] : context.DirectiveText.AsSpan((i + 1)..).TrimStart();
        if (i < 0 || secondPart.IsWhiteSpace())
        {
            return (firstPart.ToString(), null);
        }

        return (firstPart.ToString(), secondPart.ToString());
    }

    public abstract override string ToString();

    /// <summary>
    /// <c>#!</c> directive.
    /// </summary>
    public sealed class Shebang(in ParseInfo info) : CSharpDirective(info)
    {
        public override string ToString() => "#!";
    }

    public abstract class Named(in ParseInfo info) : CSharpDirective(info)
    {
        public required string Name { get; init; }
    }

    /// <summary>
    /// <c>#:sdk</c> directive.
    /// </summary>
    public sealed class Sdk(in ParseInfo info) : Named(info)
    {
        public string? Version { get; init; }

        public static new Sdk? Parse(in ParseContext context)
        {
            if (ParseOptionalTwoParts(context, separator: '@') is not var (sdkName, sdkVersion))
            {
                return null;
            }

            return new Sdk(context.Info)
            {
                Name = sdkName,
                Version = sdkVersion,
            };
        }

        public override string ToString() => Version is null ? $"#:sdk {Name}" : $"#:sdk {Name}@{Version}";
    }

    /// <summary>
    /// <c>#:property</c> directive.
    /// </summary>
    public sealed class Property(in ParseInfo info) : Named(info)
    {
        public required string Value { get; init; }

        public static new Property? Parse(in ParseContext context)
        {
            if (ParseOptionalTwoParts(context, separator: '=') is not var (propertyName, propertyValue))
            {
                return null;
            }

            if (propertyValue is null)
            {
                return context.Diagnostics.AddError<Property?>(context.SourceFile, context.Info.Span, location => string.Format(CliCommandStrings.PropertyDirectiveMissingParts, location));
            }

            try
            {
                propertyName = XmlConvert.VerifyName(propertyName);
            }
            catch (XmlException ex)
            {
                return context.Diagnostics.AddError<Property?>(context.SourceFile, context.Info.Span, location => string.Format(CliCommandStrings.PropertyDirectiveInvalidName, location, ex.Message), ex);
            }

            return new Property(context.Info)
            {
                Name = propertyName,
                Value = propertyValue,
            };
        }

        public override string ToString() => $"#:property {Name}={Value}";
    }

    /// <summary>
    /// <c>#:package</c> directive.
    /// </summary>
    public sealed class Package(in ParseInfo info) : Named(info)
    {
        public string? Version { get; init; }

        public static new Package? Parse(in ParseContext context)
        {
            if (ParseOptionalTwoParts(context, separator: '@') is not var (packageName, packageVersion))
            {
                return null;
            }

            return new Package(context.Info)
            {
                Name = packageName,
                Version = packageVersion,
            };
        }

        public override string ToString() => Version is null ? $"#:package {Name}" : $"#:package {Name}@{Version}";
    }

    /// <summary>
    /// <c>#:project</c> directive.
    /// </summary>
    public sealed class Project(in ParseInfo info) : Named(info)
    {
        public static new Project? Parse(in ParseContext context)
        {
            var directiveText = context.DirectiveText;
            if (directiveText.IsWhiteSpace())
            {
                string directiveKind = context.DirectiveKind;
                return context.Diagnostics.AddError<Project?>(context.SourceFile, context.Info.Span, location => string.Format(CliCommandStrings.MissingDirectiveName, directiveKind, location));
            }

            try
            {
                // If the path is a directory like '../lib', transform it to a project file path like '../lib/lib.csproj'.
                // Also normalize blackslashes to forward slashes to ensure the directive works on all platforms.
                var sourceDirectory = Path.GetDirectoryName(context.SourceFile.Path) ?? ".";
                var resolvedProjectPath = Path.Combine(sourceDirectory, directiveText.Replace('\\', '/'));
                if (Directory.Exists(resolvedProjectPath))
                {
                    var fullFilePath = MsbuildProject.GetProjectFileFromDirectory(resolvedProjectPath).FullName;
                    directiveText = Path.GetRelativePath(relativeTo: sourceDirectory, fullFilePath);
                }
                else if (!File.Exists(resolvedProjectPath))
                {
                    throw new GracefulException(CliStrings.CouldNotFindProjectOrDirectory, resolvedProjectPath);
                }
            }
            catch (GracefulException e)
            {
                context.Diagnostics.AddError(context.SourceFile, context.Info.Span, location => string.Format(CliCommandStrings.InvalidProjectDirective, location, e.Message), e);
            }

            return new Project(context.Info)
            {
                Name = directiveText,
            };
        }

        public override string ToString() => $"#:project {Name}";
    }
}

/// <summary>
/// Used for deduplication - compares directives by their type and name (ignoring case).
/// </summary>
internal sealed class NamedDirectiveComparer : IEqualityComparer<CSharpDirective.Named>
{
    public static readonly NamedDirectiveComparer Instance = new();

    private NamedDirectiveComparer() { }

    public bool Equals(CSharpDirective.Named? x, CSharpDirective.Named? y)
    {
        if (ReferenceEquals(x, y)) return true;

        if (x is null || y is null) return false;

        return x.GetType() == y.GetType() &&
            string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(CSharpDirective.Named obj)
    {
        return HashCode.Combine(
            obj.GetType().GetHashCode(),
            obj.Name.GetHashCode(StringComparison.OrdinalIgnoreCase));
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

internal readonly struct DiagnosticBag
{
    public bool IgnoreDiagnostics { get; private init; }

    /// <summary>
    /// If <see langword="null"/> and <see cref="IgnoreDiagnostics"/> is <see langword="false"/>, the first diagnostic is thrown as <see cref="GracefulException"/>.
    /// </summary>
    public ImmutableArray<SimpleDiagnostic>.Builder? Builder { get; private init; }

    public static DiagnosticBag ThrowOnFirst() => default;
    public static DiagnosticBag Collect(out ImmutableArray<SimpleDiagnostic>.Builder builder) => new() { Builder = builder = ImmutableArray.CreateBuilder<SimpleDiagnostic>() };
    public static DiagnosticBag Ignore() => new() { IgnoreDiagnostics = true, Builder = null };

    public void AddError(SourceFile sourceFile, TextSpan span, Func<string, string> messageFactory, Exception? inner = null)
    {
        if (Builder != null)
        {
            Debug.Assert(!IgnoreDiagnostics);
            Builder.Add(new SimpleDiagnostic { Location = sourceFile.GetFileLinePositionSpan(span), Message = messageFactory(sourceFile.GetLocationString(span)) });
        }
        else if (!IgnoreDiagnostics)
        {
            throw new GracefulException(messageFactory(sourceFile.GetLocationString(span)), inner);
        }
    }

    public T? AddError<T>(SourceFile sourceFile, TextSpan span, Func<string, string> messageFactory, Exception? inner = null)
    {
        AddError(sourceFile, span, messageFactory, inner);
        return default;
    }
}

internal sealed class RunFileBuildCacheEntry
{
    private static StringComparer GlobalPropertiesComparer => StringComparer.OrdinalIgnoreCase;
    private static StringComparer ImplicitBuildFilesComparer => StringComparer.Ordinal;

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, string> GlobalProperties { get; }

    /// <summary>
    /// Maps full path to <see cref="FileSystemInfo.LastWriteTimeUtc"/>.
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, DateTime> ImplicitBuildFiles { get; }

    [JsonConstructor]
    public RunFileBuildCacheEntry()
    {
        GlobalProperties = new(GlobalPropertiesComparer);
        ImplicitBuildFiles = new(ImplicitBuildFilesComparer);
    }

    public RunFileBuildCacheEntry(Dictionary<string, string> globalProperties)
    {
        Debug.Assert(globalProperties.Comparer == GlobalPropertiesComparer);
        GlobalProperties = globalProperties;
        ImplicitBuildFiles = new(ImplicitBuildFilesComparer);
    }
}

[JsonSerializable(typeof(RunFileBuildCacheEntry))]
[JsonSerializable(typeof(RunFileArtifactsMetadata))]
internal partial class RunFileJsonSerializerContext : JsonSerializerContext;

[Flags]
internal enum AppKinds
{
    None = 0,
    ProjectBased = 1 << 0,
    FileBased = 1 << 1,
    Any = ProjectBased | FileBased,
}
