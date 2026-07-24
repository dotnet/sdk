// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.NET.Sdk.Razor.SourceGenerators;

namespace Microsoft.NET.Sdk.Razor.Tool;

/// <summary>
/// Shared infrastructure for driving the Razor source generator in-process, so the
/// <c>generate</c> and <c>discover</c> commands can produce the same output as an ordinary
/// .NET 6+ build instead of calling the Razor engine directly.
/// </summary>
internal static class RazorSourceGeneratorHost
{
    /// <summary>
    /// Builds an <see cref="AnalyzerConfigOptionsProvider"/> that surfaces the MSBuild-derived
    /// build properties and per-file metadata the source generator reads. The keys mirror what the
    /// Razor SDK targets pass via <c>CompilerVisibleProperty</c>/<c>CompilerVisibleItemMetadata</c>.
    /// </summary>
    public static RazorAnalyzerConfigOptionsProvider CreateOptionsProvider(
        string razorConfiguration,
        string razorLanguageVersion,
        string rootNamespace,
        bool supportLocalizedComponentNames,
        bool generateMetadataSourceChecksumAttributes,
        string projectDirectory,
        IReadOnlyList<RazorInputFile> files)
    {
        var globalOptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["build_property.RazorConfiguration"] = razorConfiguration,
            ["build_property.RazorLangVersion"] = razorLanguageVersion,
            ["build_property.RootNamespace"] = rootNamespace,
            ["build_property.SupportLocalizedComponentNames"] = Bool(supportLocalizedComponentNames),
            ["build_property.GenerateRazorMetadataSourceChecksumAttributes"] = Bool(generateMetadataSourceChecksumAttributes),
            ["build_property.MSBuildProjectDirectory"] = projectDirectory,
        };

        var fileOptions = new Dictionary<string, AnalyzerConfigOptions>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // The generator base64-decodes TargetPath, matching the EncodeRazorInputItem task.
                ["build_metadata.AdditionalFiles.TargetPath"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(file.TargetPath)),
            };

            if (!string.IsNullOrEmpty(file.CssScope))
            {
                metadata["build_metadata.AdditionalFiles.CssScope"] = file.CssScope;
            }

            fileOptions[file.SourcePath] = new RazorAnalyzerConfigOptions(metadata);
        }

        return new RazorAnalyzerConfigOptionsProvider(new RazorAnalyzerConfigOptions(globalOptions), fileOptions);
    }

    /// <summary>
    /// Turns the input files into <see cref="AdditionalText"/> entries the generator treats as Razor sources.
    /// </summary>
    public static ImmutableArray<AdditionalText> CreateAdditionalTexts(IReadOnlyList<RazorInputFile> files)
    {
        var builder = ImmutableArray.CreateBuilder<AdditionalText>(files.Count);
        foreach (var file in files)
        {
            builder.Add(new RazorSourceGeneratorAdditionalText(file.SourcePath, file.Text));
        }

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Creates the compilation whose references the generator scans for tag helpers. It has no syntax
    /// trees of its own; the generator adds the generated component declarations internally.
    /// </summary>
    public static CSharpCompilation CreateCompilation(
        IEnumerable<string> referencePaths,
        Func<string, MetadataReferenceProperties, PortableExecutableReference> referenceProvider,
        string assemblyName = "RazorGeneration")
    {
        var references = referencePaths.Select(path => (MetadataReference)referenceProvider(path, default));
        return CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: null,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Creates a generator driver hosting the Razor source generator. Host outputs are left enabled
    /// (nothing disabled) so <c>discover</c> can read the <c>RazorGeneratorResult</c> host output.
    /// </summary>
    public static GeneratorDriver CreateDriver(CSharpParseOptions parseOptions)
    {
        var generator = new RazorSourceGenerator().AsSourceGenerator();
        var driverOptions = new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: false);
        return CSharpGeneratorDriver.Create([generator], parseOptions: parseOptions, driverOptions: driverOptions);
    }

    public static CSharpParseOptions CreateParseOptions(LanguageVersion languageVersion)
        => new(languageVersion);

    private static string Bool(bool value) => value ? "true" : "false";
}

/// <summary>
/// A Razor source file handed to the generator: the physical path on disk, the project-relative
/// target path (which drives the generated hint name and namespace), and an optional CSS scope.
/// </summary>
internal readonly struct RazorInputFile
{
    public RazorInputFile(string sourcePath, string targetPath, string cssScope = null, SourceText text = null)
    {
        SourcePath = sourcePath;
        TargetPath = targetPath;
        CssScope = cssScope;
        Text = text;
    }

    public string SourcePath { get; }

    public string TargetPath { get; }

    public string CssScope { get; }

    /// <summary>In-memory content, used for synthetic inputs; when null the file is read from <see cref="SourcePath"/>.</summary>
    public SourceText Text { get; }
}

internal sealed class RazorSourceGeneratorAdditionalText : AdditionalText
{
    private readonly SourceText _text;

    public RazorSourceGeneratorAdditionalText(string path, SourceText text = null)
    {
        Path = path;
        _text = text;
    }

    public override string Path { get; }

    public override SourceText GetText(CancellationToken cancellationToken = default)
    {
        if (_text is not null)
        {
            return _text;
        }

        using var stream = File.OpenRead(Path);
        return SourceText.From(stream, Encoding.UTF8);
    }
}

internal sealed class RazorAnalyzerConfigOptions : AnalyzerConfigOptions
{
    private readonly Dictionary<string, string> _options;

    public RazorAnalyzerConfigOptions(Dictionary<string, string> options)
    {
        _options = options;
    }

    public override bool TryGetValue(string key, out string value) => _options.TryGetValue(key, out value);

    public override IEnumerable<string> Keys => _options.Keys;
}

internal sealed class RazorAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private static readonly AnalyzerConfigOptions s_empty = new RazorAnalyzerConfigOptions(new Dictionary<string, string>(StringComparer.Ordinal));

    private readonly Dictionary<string, AnalyzerConfigOptions> _fileOptions;

    public RazorAnalyzerConfigOptionsProvider(AnalyzerConfigOptions globalOptions, Dictionary<string, AnalyzerConfigOptions> fileOptions)
    {
        GlobalOptions = globalOptions;
        _fileOptions = fileOptions;
    }

    public override AnalyzerConfigOptions GlobalOptions { get; }

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => s_empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        => _fileOptions.TryGetValue(textFile.Path, out var options) ? options : s_empty;
}
