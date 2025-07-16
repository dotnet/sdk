// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
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
    public bool NoCache { get; init; }
    public bool NoBuild { get; init; }

    /// <summary>
    /// If <see langword="true"/>, no build markers are written
    /// (like <see cref="BuildStartCacheFileName"/> and <see cref="BuildSuccessCacheFileName"/>).
    /// </summary>
    public bool NoBuildMarkers { get; init; }

    public ImmutableArray<CSharpDirective> Directives
    {
        get
        {
            if (field.IsDefault)
            {
                var sourceFile = LoadSourceFile(EntryPointFileFullPath);
                field = FindDirectives(sourceFile, reportAllErrors: false, errors: null);
                Debug.Assert(!field.IsDefault);
            }

            return field;
        }

        init;
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

                return 0;
            }

            MarkBuildStart();
        }

        Dictionary<string, string?> savedEnvironmentVariables = [];
        ProjectCollection? projectCollection = null;
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
            projectCollection = new ProjectCollection(
                MSBuildArgs.GlobalProperties,
                [.. binaryLoggers, consoleLogger],
                ToolsetDefinitionLocations.Default);
            var parameters = new BuildParameters(projectCollection)
            {
                Loggers = projectCollection.Loggers,
                LogTaskInputs = binaryLoggers.Length != 0,
            };

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

                BuildManager.DefaultBuildManager.BeginBuild(parameters);

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

                // For some reason we need to BeginBuild after creating BuildRequestData otherwise the binlog doesn't contain Evaluation.
                if (NoRestore)
                {
                    BuildManager.DefaultBuildManager.BeginBuild(parameters);
                }

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

            binaryLogger?.Shutdown();
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

        static ILogger? GetBinaryLogger(IReadOnlyList<string>? args)
        {
            if (args is null) return null;
            // Like in MSBuild, only the last binary logger is used.
            for (int i = args.Count - 1; i >= 0; i--)
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

    private void MarkBuildStart()
    {
        if (NoBuildMarkers)
        {
            return;
        }

        string directory = GetArtifactsPath();

        CreateTempSubdirectory(directory);

        File.WriteAllText(Path.Join(directory, BuildStartCacheFileName), EntryPointFileFullPath);
    }

    private void MarkBuildSuccess(RunFileBuildCacheEntry cacheEntry)
    {
        if (NoBuildMarkers)
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

        return GetTempSubdirectory(directoryName);
    }

    /// <summary>
    /// Obtains a temporary subdirectory for file-based apps.
    /// </summary>
    public static string GetTempSubdirectory(string name)
    {
        // We want a location where permissions are expected to be restricted to the current user.
        string directory = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.GetTempPath()
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return Path.Join(directory, "dotnet", "runfile", name);
    }

    /// <summary>
    /// Creates a temporary subdirectory for file-based apps.
    /// Use <see cref="GetTempSubdirectory"/> to obtain the path.
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

        // Kept in sync with the default `dotnet new console` project file (enforced by `DotnetProjectAddTests.SameAsTemplate`).
        writer.WriteLine($"""
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <PublishAot>true</PublishAot>
              </PropertyGroup>
            """);

        if (isVirtualProject)
        {
            writer.WriteLine("""

                  <PropertyGroup>
                    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                  </PropertyGroup>
                """);
        }

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

        if (isVirtualProject)
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

            writer.WriteLine("  </ItemGroup>");
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

            writer.WriteLine();
            writer.WriteLine(TargetOverrides);
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

        var deduplicated = new HashSet<CSharpDirective.Named>(NamedDirectiveComparer.Instance);
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
                    // If the directive is already present, report an error.
                    if (deduplicated.TryGetValue(directive, out var existingDirective))
                    {
                        var typeAndName = $"#:{existingDirective.GetType().Name.ToLowerInvariant()} {existingDirective.Name}";
                        if (errors != null)
                        {
                            errors.Add(new SimpleDiagnostic
                            {
                                Location = sourceFile.GetFileLinePositionSpan(directive.Span),
                                Message = string.Format(CliCommandStrings.DuplicateDirective, typeAndName, sourceFile.GetLocationString(directive.Span)),
                            });
                        }
                        else
                        {
                            throw new GracefulException(CliCommandStrings.DuplicateDirective, typeAndName, sourceFile.GetLocationString(directive.Span));
                        }
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

    public static void RemoveDirectivesFromFile(ImmutableArray<CSharpDirective> directives, SourceText text, string filePath)
    {
        if (RemoveDirectivesFromFile(directives, text) is { } modifiedText)
        {
            using var stream = File.Open(filePath, FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            modifiedText.Write(writer);
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

/// <summary>
/// Represents a C# directive starting with <c>#:</c> (a.k.a., "file-level directive").
/// Those are ignored by the language but recognized by us.
/// </summary>
internal abstract class CSharpDirective
{
    private CSharpDirective() { }

    /// <summary>
    /// Span of the full line including the trailing line break.
    /// </summary>
    public required TextSpan Span { get; init; }

    public static Named? Parse(ImmutableArray<SimpleDiagnostic>.Builder? errors, SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText)
    {
        return directiveKind switch
        {
            "sdk" => Sdk.Parse(errors, sourceFile, span, directiveKind, directiveText),
            "property" => Property.Parse(errors, sourceFile, span, directiveKind, directiveText),
            "package" => Package.Parse(errors, sourceFile, span, directiveKind, directiveText),
            "project" => Project.Parse(errors, sourceFile, span, directiveKind, directiveText),
            _ => ReportError<Named>(errors, sourceFile, span, string.Format(CliCommandStrings.UnrecognizedDirective, directiveKind, sourceFile.GetLocationString(span))),
        };
    }

    private static T? ReportError<T>(ImmutableArray<SimpleDiagnostic>.Builder? errors, SourceFile sourceFile, TextSpan span, string message, Exception? inner = null)
    {
        ReportError(errors, sourceFile, span, message, inner);
        return default;
    }

    private static void ReportError(ImmutableArray<SimpleDiagnostic>.Builder? errors, SourceFile sourceFile, TextSpan span, string message, Exception? inner = null)
    {
        if (errors != null)
        {
            errors.Add(new SimpleDiagnostic { Location = sourceFile.GetFileLinePositionSpan(span), Message = message });
        }
        else
        {
            throw new GracefulException(message, inner);
        }
    }

    private static (string, string?)? ParseOptionalTwoParts(ImmutableArray<SimpleDiagnostic>.Builder? errors, SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText, char separator)
    {
        var i = directiveText.IndexOf(separator, StringComparison.Ordinal);
        var firstPart = (i < 0 ? directiveText : directiveText.AsSpan(..i)).TrimEnd();

        if (firstPart.IsWhiteSpace())
        {
            return ReportError<(string, string?)?>(errors, sourceFile, span, string.Format(CliCommandStrings.MissingDirectiveName, directiveKind, sourceFile.GetLocationString(span)));
        }

        // If the name contains characters that resemble separators, report an error to avoid any confusion.
        if (Patterns.DisallowedNameCharacters.IsMatch(firstPart))
        {
            return ReportError<(string, string?)?>(errors, sourceFile, span, string.Format(CliCommandStrings.InvalidDirectiveName, directiveKind, separator, sourceFile.GetLocationString(span)));
        }

        var secondPart = i < 0 ? [] : directiveText.AsSpan((i + 1)..).TrimStart();
        if (i < 0 || secondPart.IsWhiteSpace())
        {
            return (firstPart.ToString(), null);
        }

        return (firstPart.ToString(), secondPart.ToString());
    }

    /// <summary>
    /// <c>#!</c> directive.
    /// </summary>
    public sealed class Shebang : CSharpDirective;

    public abstract class Named : CSharpDirective
    {
        public required string Name { get; init; }
    }

    /// <summary>
    /// <c>#:sdk</c> directive.
    /// </summary>
    public sealed class Sdk : Named
    {
        private Sdk() { }

        public string? Version { get; init; }

        public static new Sdk? Parse(ImmutableArray<SimpleDiagnostic>.Builder? errors, SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText)
        {
            if (ParseOptionalTwoParts(errors, sourceFile, span, directiveKind, directiveText, separator: '@') is not var (sdkName, sdkVersion))
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
    }

    /// <summary>
    /// <c>#:property</c> directive.
    /// </summary>
    public sealed class Property : Named
    {
        private Property() { }

        public required string Value { get; init; }

        public static new Property? Parse(ImmutableArray<SimpleDiagnostic>.Builder? errors, SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText)
        {
            if (ParseOptionalTwoParts(errors, sourceFile, span, directiveKind, directiveText, separator: '=') is not var (propertyName, propertyValue))
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
    public sealed class Package : Named
    {
        private Package() { }

        public string? Version { get; init; }

        public static new Package? Parse(ImmutableArray<SimpleDiagnostic>.Builder? errors, SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText)
        {
            if (ParseOptionalTwoParts(errors, sourceFile, span, directiveKind, directiveText, separator: '@') is not var (packageName, packageVersion))
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

    /// <summary>
    /// <c>#:project</c> directive.
    /// </summary>
    public sealed class Project : Named
    {
        private Project() { }

        public static new Project? Parse(ImmutableArray<SimpleDiagnostic>.Builder? errors, SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText)
        {
            if (directiveText.IsWhiteSpace())
            {
                return ReportError<Project?>(errors, sourceFile, span, string.Format(CliCommandStrings.MissingDirectiveName, directiveKind, sourceFile.GetLocationString(span)));
            }

            try
            {
                // If the path is a directory like '../lib', transform it to a project file path like '../lib/lib.csproj'.
                // Also normalize blackslashes to forward slashes to ensure the directive works on all platforms.
                var sourceDirectory = Path.GetDirectoryName(sourceFile.Path) ?? ".";
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
                ReportError(errors, sourceFile, span, string.Format(CliCommandStrings.InvalidProjectDirective, sourceFile.GetLocationString(span), e.Message), e);
            }

            return new Project
            {
                Span = span,
                Name = directiveText,
            };
        }
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
internal partial class RunFileJsonSerializerContext : JsonSerializerContext;
