// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Security;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using LocalizableStrings = Microsoft.DotNet.Tools.Run.LocalizableStrings;

namespace Microsoft.DotNet.Tools;

/// <summary>
/// Used to build a virtual project file in memory to support <c>dotnet run file.cs</c>.
/// </summary>
internal sealed class VirtualProjectBuildingCommand
{
    private ImmutableArray<CSharpDirective> _directives;
    private string? _targetFilePath;

    public Dictionary<string, string> GlobalProperties { get; } = new(StringComparer.OrdinalIgnoreCase);
    public required string EntryPointFileFullPath { get; init; }

    public int Execute(string[] binaryLoggerArgs, ILogger consoleLogger)
    {
        var binaryLogger = GetBinaryLogger(binaryLoggerArgs);
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

            // Then do a build.
            var buildRequest = new BuildRequestData(
                CreateProjectInstance(projectCollection),
                targetsToBuild: ["Build"]);
            var buildResult = BuildManager.DefaultBuildManager.BuildRequest(buildRequest);
            if (buildResult.OverallResult != BuildResultCode.Success)
            {
                return 1;
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
    /// Needs to be called before the first call to <see cref="CreateProjectInstance(ProjectCollection)"/>.
    /// </summary>
    public VirtualProjectBuildingCommand PrepareProjectInstance()
    {
        Debug.Assert(_directives.IsDefault && _targetFilePath is null, $"{nameof(PrepareProjectInstance)} should not be called multiple times.");

        var sourceFile = LoadSourceFile(EntryPointFileFullPath);
        _directives = FindDirectives(sourceFile);

        // If there were any `#:` directives, remove them from the file.
        // (This is temporary until Roslyn is updated to ignore them.)
        _targetFilePath = EntryPointFileFullPath;
        if (_directives.Length != 0)
        {
            var targetDirectory = Path.Join(Path.GetDirectoryName(_targetFilePath), "obj");
            Directory.CreateDirectory(targetDirectory);
            _targetFilePath = Path.Join(targetDirectory, Path.GetFileName(_targetFilePath));

            RemoveDirectivesFromFile(_directives, sourceFile.Text, _targetFilePath);
        }

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
            Debug.Assert(!_directives.IsDefault && _targetFilePath is not null, $"{nameof(PrepareProjectInstance)} should have been called first.");

            var projectFileFullPath = Path.ChangeExtension(EntryPointFileFullPath, ".csproj");
            var projectFileWriter = new StringWriter();
            WriteProjectFile(projectFileWriter, _directives, isVirtualProject: true, targetFilePath: _targetFilePath);
            var projectFileText = projectFileWriter.ToString();

            using var reader = new StringReader(projectFileText);
            using var xmlReader = XmlReader.Create(reader);
            var projectRoot = ProjectRootElement.Create(xmlReader, projectCollection);
            projectRoot.FullPath = projectFileFullPath;
            return projectRoot;
        }
    }

    public static void WriteProjectFile(TextWriter writer, ImmutableArray<CSharpDirective> directives)
    {
        WriteProjectFile(writer, directives, isVirtualProject: false, targetFilePath: null);
    }

    private static void WriteProjectFile(TextWriter writer, ImmutableArray<CSharpDirective> directives, bool isVirtualProject, string? targetFilePath)
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
            writer.WriteLine($"""
                <Project>

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

    public static ImmutableArray<CSharpDirective> FindDirectives(SourceFile sourceFile)
    {
        var builder = ImmutableArray.CreateBuilder<CSharpDirective>();

        // NOTE: When Roslyn is updated to support "ignored directives", we should use its SyntaxTokenParser instead.
        foreach (var line in sourceFile.Text.Lines)
        {
            var lineText = sourceFile.Text.ToString(line.Span);

            if (Patterns.Shebang.IsMatch(lineText))
            {
                builder.Add(new CSharpDirective.Shebang { Span = line.SpanIncludingLineBreak });
            }
            else if (Patterns.Directive.Match(lineText) is { Success: true } match)
            {
                builder.Add(CSharpDirective.Parse(sourceFile, line.SpanIncludingLineBreak, match.Groups[1].Value, match.Groups[2].Value));
            }
        }

        // The result should be ordered by source location, RemoveDirectivesFromFile depends on that.
        return builder.ToImmutable();
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
    [GeneratedRegex("""^\s*#:\s*(\w*)\s*(.*?)\s*$""")]
    public static partial Regex Directive { get; }

    [GeneratedRegex("""^\s*#!.*$""")]
    public static partial Regex Shebang { get; }
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
            _ => throw new GracefulException(LocalizableStrings.UnrecognizedDirective, directiveKind, sourceFile.GetLocationString(span)),
        };
    }

    private static (string, string?) ParseOptionalTwoParts(SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText)
    {
        var i = directiveText.IndexOf(' ', StringComparison.Ordinal);
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
                throw new GracefulException(LocalizableStrings.MissingDirectiveName, directiveKind, sourceFile.GetLocationString(span));
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
                throw new GracefulException(LocalizableStrings.PropertyDirectiveMissingParts, sourceFile.GetLocationString(span));
            }

            try
            {
                propertyName = XmlConvert.VerifyName(propertyName);
            }
            catch (XmlException ex)
            {
                throw new GracefulException(string.Format(LocalizableStrings.PropertyDirectiveInvalidName, sourceFile.GetLocationString(span), ex.Message), ex);
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
        private Package() { }

        public required string Name { get; init; }
        public string? Version { get; init; }

        public static new Package Parse(SourceFile sourceFile, TextSpan span, string directiveKind, string directiveText)
        {
            var (packageName, packageVersion) = ParseOptionalTwoParts(sourceFile, span, directiveKind, directiveText);

            return new Package
            {
                Span = span,
                Name = packageName,
                Version = packageVersion,
            };
        }
    }
}
