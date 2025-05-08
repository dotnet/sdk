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

    private ImmutableArray<CSharpDirective> _directives;

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

        RunFileBuildCacheEntry? cacheEntry = null;

        if (!NoBuild)
        {
            if (NoCache)
            {
                if (NoRestore)
                {
                    throw new GracefulException(CliCommandStrings.InvalidOptionCombination, RunCommandParser.NoCacheOption.Name, RunCommandParser.NoRestoreOption.Name);
                }

                cacheEntry = ComputeCacheEntry(out _);
            }
            else if (!NeedsToBuild(out cacheEntry))
            {
                if (binaryLogger is not null)
                {
                    Reporter.Output.WriteLine(CliCommandStrings.NoBinaryLogBecauseUpToDate.Yellow());
                }

                PrepareProjectInstance();

                return 0;
            }

            MarkBuildStart();
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

            PrepareProjectInstance();

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
    /// Compute current cache entry - we need to do this always:
    /// <list type="bullet">
    /// <item>if we can skip build, we still need to check everything in the cache entry (e.g., implicit build files)</item>
    /// <item>if we have to build, we need to have the cache entry to write it to the success cache file</item>
    /// </list>
    /// </summary>
    private RunFileBuildCacheEntry ComputeCacheEntry(out FileInfo entryPointFileInfo)
    {
        var cacheEntry = new RunFileBuildCacheEntry(GlobalProperties);
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
        string directory = GetArtifactsPath();
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Join(directory, BuildStartCacheFileName), EntryPointFileFullPath);
    }

    private void MarkBuildSuccess(RunFileBuildCacheEntry cacheEntry)
    {
        string successCacheFile = Path.Join(GetArtifactsPath(), BuildSuccessCacheFileName);
        using var stream = File.Open(successCacheFile, FileMode.Create, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, cacheEntry, RunFileJsonSerializerContext.Default.RunFileBuildCacheEntry);
    }

    /// <summary>
    /// Needs to be called before the first call to <see cref="CreateProjectInstance(ProjectCollection)"/>.
    /// </summary>
    public VirtualProjectBuildingCommand PrepareProjectInstance()
    {
        Debug.Assert(_directives.IsDefault, $"{nameof(PrepareProjectInstance)} should not be called multiple times.");

        var sourceFile = LoadSourceFile(EntryPointFileFullPath);
        _directives = FindDirectives(sourceFile, reportErrors: false);

        return this;
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
            Debug.Assert(!_directives.IsDefault, $"{nameof(PrepareProjectInstance)} should have been called first.");

            var projectFileFullPath = Path.ChangeExtension(EntryPointFileFullPath, ".csproj");
            var projectFileWriter = new StringWriter();
            WriteProjectFile(
                projectFileWriter,
                _directives,
                isVirtualProject: true,
                targetFilePath: EntryPointFileFullPath,
                artifactsPath: GetArtifactsPath());
            var projectFileText = projectFileWriter.ToString();

            using var reader = new StringReader(projectFileText);
            using var xmlReader = XmlReader.Create(reader);
            var projectRoot = ProjectRootElement.Create(xmlReader, projectCollection);
            projectRoot.FullPath = projectFileFullPath;
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

    public static void WriteProjectFile(TextWriter writer, ImmutableArray<CSharpDirective> directives)
    {
        WriteProjectFile(writer, directives, isVirtualProject: false, targetFilePath: null, artifactsPath: null);
    }

    private static void WriteProjectFile(
        TextWriter writer,
        ImmutableArray<CSharpDirective> directives,
        bool isVirtualProject,
        string? targetFilePath,
        string? artifactsPath)
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

        if (isVirtualProject)
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
            if (isVirtualProject)
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

        if (processedDirectives > 1)
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

        if (isVirtualProject)
        {
            writer.WriteLine("""

                  <PropertyGroup>
                    <EnableDefaultItems>false</EnableDefaultItems>
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

        Debug.Assert(processedDirectives + directives.OfType<CSharpDirective.Shebang>().Count() == directives.Length);

        if (isVirtualProject)
        {
            Debug.Assert(targetFilePath is not null);

            writer.WriteLine($"""

                  <ItemGroup>
                    <Compile Include="{EscapeValue(targetFilePath)}" />
                  </ItemGroup>

                """);

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

    public static ImmutableArray<CSharpDirective> FindDirectivesForConversion(SourceFile sourceFile, bool force)
    {
        return FindDirectives(sourceFile, reportErrors: !force);
    }

#pragma warning disable RSEXPERIMENTAL003 // 'SyntaxTokenParser' is experimental
    private static ImmutableArray<CSharpDirective> FindDirectives(SourceFile sourceFile, bool reportErrors)
    {
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
                builder.Add(CSharpDirective.Parse(sourceFile, span, name.ToString(), value.ToString()));
            }

            previousWhiteSpaceSpan = default;
        }

        // In conversion mode, we want to report errors for any invalid directives in the rest of the file
        // so users don't end up with invalid directives in the converted project.
        if (reportErrors)
        {
            tokenizer.ResetTo(result);

            do
            {
                result = tokenizer.ParseNextToken();

                foreach (var trivia in result.Token.LeadingTrivia)
                {
                    reportErrorFor(sourceFile, trivia);
                }

                foreach (var trivia in result.Token.TrailingTrivia)
                {
                    reportErrorFor(sourceFile, trivia);
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

        static void reportErrorFor(SourceFile sourceFile, SyntaxTrivia trivia)
        {
            if (trivia.ContainsDiagnostics && trivia.IsKind(SyntaxKind.IgnoredDirectiveTrivia))
            {
                throw new GracefulException(CliCommandStrings.CannotConvertDirective, sourceFile.GetLocationString(trivia.Span));
            }
        }
    }
#pragma warning restore RSEXPERIMENTAL003 // 'SyntaxTokenParser' is experimental

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
        return entryPointFilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && File.Exists(entryPointFilePath);
    }
}

internal readonly record struct SourceFile(string Path, SourceText Text)
{
    public string GetLocationString(TextSpan span)
    {
        var positionSpan = new FileLinePositionSpan(Path, Text.Lines.GetLinePositionSpan(span));
        return $"{positionSpan.Path}:{positionSpan.StartLinePosition.Line + 1}";
    }
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

    public static CSharpDirective Parse(SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText)
    {
        return directiveKind switch
        {
            "sdk" => Sdk.Parse(sourceFile, span, directiveKind, directiveText),
            "property" => Property.Parse(sourceFile, span, directiveKind, directiveText),
            "package" => Package.Parse(sourceFile, span, directiveKind, directiveText),
            _ => throw new GracefulException(CliCommandStrings.UnrecognizedDirective, directiveKind, sourceFile.GetLocationString(span)),
        };
    }

    private static (string, string?) ParseOptionalTwoParts(SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText, SearchValues<char>? separators = null)
    {
        var i = separators != null
            ? directiveText.AsSpan().IndexOfAny(separators)
            : directiveText.IndexOf(' ', StringComparison.Ordinal);
        var firstPart = checkFirstPart(i < 0 ? directiveText : directiveText[..i]);
        var secondPart = i < 0 ? [] : directiveText.AsSpan((i + 1)..).TrimStart();
        if (i < 0 || secondPart.IsWhiteSpace())
        {
            return (firstPart, null);
        }

        return (firstPart, secondPart.ToString());

        string checkFirstPart(string firstPart)
        {
            if (string.IsNullOrWhiteSpace(firstPart))
            {
                throw new GracefulException(CliCommandStrings.MissingDirectiveName, directiveKind, sourceFile.GetLocationString(span));
            }

            return firstPart;
        }
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

        public static new Sdk Parse(SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText)
        {
            var (sdkName, sdkVersion) = ParseOptionalTwoParts(sourceFile, span, directiveKind, directiveText);

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

        public static new Property Parse(SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText)
        {
            var (propertyName, propertyValue) = ParseOptionalTwoParts(sourceFile, span, directiveKind, directiveText);

            if (propertyValue is null)
            {
                throw new GracefulException(CliCommandStrings.PropertyDirectiveMissingParts, sourceFile.GetLocationString(span));
            }

            try
            {
                propertyName = XmlConvert.VerifyName(propertyName);
            }
            catch (XmlException ex)
            {
                throw new GracefulException(string.Format(CliCommandStrings.PropertyDirectiveInvalidName, sourceFile.GetLocationString(span), ex.Message), ex);
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

        public static new Package Parse(SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText)
        {
            var (packageName, packageVersion) = ParseOptionalTwoParts(sourceFile, span, directiveKind, directiveText, s_separators);

            return new Package
            {
                Span = span,
                Name = packageName,
                Version = packageVersion,
            };
        }
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
