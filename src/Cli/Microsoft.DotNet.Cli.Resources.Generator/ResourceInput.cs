// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.DotNet.Cli.Resources.Generator;

/// <summary>
///  Captures one additional resource file and its compiler-visible metadata.
/// </summary>
internal sealed class ResourceInput : IEquatable<ResourceInput>
{
    private const string MetadataPrefix = "build_metadata.AdditionalFiles.";

    /// <summary>
    ///  The maximum number of source characters accepted from one resource file.
    /// </summary>
    internal const int MaxSourceCharacters = 8 * 1024 * 1024;

    private ResourceInput(
        string path,
        SourceText? sourceText,
        string? content,
        int contentLength,
        string? rootNamespace,
        string? generator,
        string? cliResourceGenerateSource,
        string? cliResourceFamily,
        string? culture,
        string? withCulture,
        string? link,
        string? relativeDir,
        string? className,
        string? manifestResourceName,
        string? targetPath,
        string? includeDefaultValues,
        string? emitFormatMethods,
        string? isPublic,
        string? omitGetResourceString,
        string? asConstants,
        string? noWarn)
    {
        Path = path;
        SourceText = sourceText;
        Content = content;
        ContentLength = contentLength;
        RootNamespace = rootNamespace;
        Generator = generator;
        CliResourceGenerateSource = cliResourceGenerateSource;
        CliResourceFamily = cliResourceFamily;
        Culture = culture;
        WithCulture = withCulture;
        Link = link;
        RelativeDir = relativeDir;
        ClassName = className;
        ManifestResourceName = manifestResourceName;
        TargetPath = targetPath;
        IncludeDefaultValues = includeDefaultValues;
        EmitFormatMethods = emitFormatMethods;
        Public = isPublic;
        OmitGetResourceString = omitGetResourceString;
        AsConstants = asConstants;
        NoWarn = noWarn;
    }

    /// <summary>
    ///  Gets the resource file path.
    /// </summary>
    internal string Path { get; }

    private SourceText? SourceText { get; }

    /// <summary>
    ///  Gets the resource text when it is available and within the size limit.
    /// </summary>
    internal string? Content { get; }

    /// <summary>
    ///  Gets the source length, or <c>-1</c> when the compiler did not provide source text.
    /// </summary>
    internal int ContentLength { get; }

    /// <summary>
    ///  Gets the consuming project's root namespace.
    /// </summary>
    internal string? RootNamespace { get; }

    /// <summary>
    ///  Gets the selected RESX generator name.
    /// </summary>
    internal string? Generator { get; }

    /// <summary>
    ///  Gets the metadata value that opts the resource into source generation.
    /// </summary>
    internal string? CliResourceGenerateSource { get; }

    /// <summary>
    ///  Gets the precomputed neutral resource family.
    /// </summary>
    internal string? CliResourceFamily { get; }

    /// <summary>
    ///  Gets the resource culture metadata.
    /// </summary>
    internal string? Culture { get; }

    /// <summary>
    ///  Gets the metadata controlling culture suffix handling.
    /// </summary>
    internal string? WithCulture { get; }

    /// <summary>
    ///  Gets the logical linked path of the resource.
    /// </summary>
    internal string? Link { get; }

    /// <summary>
    ///  Gets the resource's relative directory metadata.
    /// </summary>
    internal string? RelativeDir { get; }

    /// <summary>
    ///  Gets the configured generated class name.
    /// </summary>
    internal string? ClassName { get; }

    /// <summary>
    ///  Gets the configured manifest resource name.
    /// </summary>
    internal string? ManifestResourceName { get; }

    /// <summary>
    ///  Gets the configured target path.
    /// </summary>
    internal string? TargetPath { get; }

    /// <summary>
    ///  Gets the metadata controlling default-value emission.
    /// </summary>
    internal string? IncludeDefaultValues { get; }

    /// <summary>
    ///  Gets the metadata controlling format-method emission.
    /// </summary>
    internal string? EmitFormatMethods { get; }

    /// <summary>
    ///  Gets the metadata controlling generated class visibility.
    /// </summary>
    internal string? Public { get; }

    /// <summary>
    ///  Gets the unsupported metadata requesting omission of resource lookup methods.
    /// </summary>
    internal string? OmitGetResourceString { get; }

    /// <summary>
    ///  Gets the unsupported metadata requesting constant generation.
    /// </summary>
    internal string? AsConstants { get; }

    /// <summary>
    ///  Gets the unsupported diagnostic-suppression metadata.
    /// </summary>
    internal string? NoWarn { get; }

    /// <summary>
    ///  Gets a value indicating whether this resource selects the .NET CLI resource generator.
    /// </summary>
    internal bool IsSelected =>
        string.Equals(Generator, "Microsoft.DotNet.Cli.Resources", StringComparison.OrdinalIgnoreCase)
            && IsTrue(CliResourceGenerateSource);

    /// <summary>
    ///  Gets a value indicating whether the resource requests an unsupported option.
    /// </summary>
    internal bool HasUnsupportedOptions =>
        IsTrue(OmitGetResourceString)
            || IsTrue(AsConstants)
            || !string.IsNullOrWhiteSpace(NoWarn);

    /// <summary>
    ///  Creates a resource input from an additional file and its analyzer configuration.
    /// </summary>
    /// <param name="file">The additional resource file.</param>
    /// <param name="optionsProvider">The analyzer configuration provider.</param>
    /// <param name="cancellationToken">The cancellation token for reading source text.</param>
    /// <returns>The captured resource input.</returns>
    internal static ResourceInput Create(
        AdditionalText file,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken)
    {
        AnalyzerConfigOptions options = optionsProvider.GetOptions(file);
        AnalyzerConfigOptions globalOptions = optionsProvider.GlobalOptions;
        SourceText? sourceText = file.GetText(cancellationToken);
        int contentLength = sourceText?.Length ?? -1;
        string? content = sourceText is not null && contentLength <= MaxSourceCharacters
            ? sourceText.ToString()
            : null;

        return new(
            file.Path,
            sourceText,
            content,
            contentLength,
            ReadOption(globalOptions, "build_property.RootNamespace"),
            ReadMetadata(options, "Generator"),
            ReadMetadata(options, "CliResourceGenerateSource"),
            ReadMetadata(options, "CliResourceFamily"),
            ReadMetadata(options, "Culture"),
            ReadMetadata(options, "WithCulture"),
            ReadMetadata(options, "Link"),
            ReadMetadata(options, "RelativeDir"),
            ReadMetadata(options, "ClassName"),
            ReadMetadata(options, "ManifestResourceName"),
            ReadMetadata(options, "TargetPath"),
            ReadMetadata(options, "IncludeDefaultValues"),
            ReadMetadata(options, "EmitFormatMethods"),
            ReadMetadata(options, "Public"),
            ReadMetadata(options, "OmitGetResourceString"),
            ReadMetadata(options, "AsConstants"),
            ReadMetadata(options, "NoWarn"));
    }

    /// <summary>
    ///  Parses an optional Boolean metadata value.
    /// </summary>
    /// <param name="value">The metadata value to parse.</param>
    /// <returns><see langword="true"/> only when the value parses as <see langword="true"/>.</returns>
    internal static bool IsTrue(string? value) =>
        bool.TryParse(value, out bool parsed) && parsed;

    /// <summary>
    ///  Creates a diagnostic location within the original resource text.
    /// </summary>
    /// <param name="line">The zero-based line, clamped to the available source.</param>
    /// <param name="column">The zero-based column, clamped to the available source line.</param>
    /// <returns>The corresponding diagnostic location.</returns>
    internal Location CreateLocation(int line, int column)
    {
        SourceText? sourceText = SourceText;
        if (sourceText is null)
        {
            return Location.Create(Path, textSpan: default, lineSpan: default);
        }

        int lineIndex = Math.Max(0, Math.Min(line, sourceText.Lines.Count - 1));
        TextLine sourceLine = sourceText.Lines[lineIndex];
        int columnIndex = Math.Max(0, Math.Min(column, sourceLine.Span.Length));
        int position = sourceLine.Start + columnIndex;
        LinePosition point = new(lineIndex, columnIndex);

        return Location.Create(
            Path,
            new TextSpan(position, 0),
            new LinePositionSpan(point, point));
    }

            /// <inheritdoc/>
    public bool Equals(ResourceInput? other)
    {
        return ReferenceEquals(this, other)
            || (other is not null
                && Path == other.Path
                && Content == other.Content
                && ContentLength == other.ContentLength
                && RootNamespace == other.RootNamespace
                && Generator == other.Generator
                && CliResourceGenerateSource == other.CliResourceGenerateSource
                && CliResourceFamily == other.CliResourceFamily
                && Culture == other.Culture
                && WithCulture == other.WithCulture
                && Link == other.Link
                && RelativeDir == other.RelativeDir
                && ClassName == other.ClassName
                && ManifestResourceName == other.ManifestResourceName
                && TargetPath == other.TargetPath
                && IncludeDefaultValues == other.IncludeDefaultValues
                && EmitFormatMethods == other.EmitFormatMethods
                && Public == other.Public
                && OmitGetResourceString == other.OmitGetResourceString
                && AsConstants == other.AsConstants
                && NoWarn == other.NoWarn);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ResourceInput);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = AddHash(17, Path);
            hash = AddHash(hash, Content);
            hash = (hash * 397) ^ ContentLength;
            hash = AddHash(hash, RootNamespace);
            hash = AddHash(hash, Generator);
            hash = AddHash(hash, CliResourceGenerateSource);
            hash = AddHash(hash, CliResourceFamily);
            hash = AddHash(hash, Culture);
            hash = AddHash(hash, WithCulture);
            hash = AddHash(hash, Link);
            hash = AddHash(hash, RelativeDir);
            hash = AddHash(hash, ClassName);
            hash = AddHash(hash, ManifestResourceName);
            hash = AddHash(hash, TargetPath);
            hash = AddHash(hash, IncludeDefaultValues);
            hash = AddHash(hash, EmitFormatMethods);
            hash = AddHash(hash, Public);
            hash = AddHash(hash, OmitGetResourceString);
            hash = AddHash(hash, AsConstants);
            return AddHash(hash, NoWarn);
        }
    }

    private static string? ReadMetadata(AnalyzerConfigOptions options, string name) =>
        ReadOption(options, MetadataPrefix + name);

    private static string? ReadOption(AnalyzerConfigOptions options, string name) =>
        options.TryGetValue(name, out string? value) ? value : null;

    private static int AddHash(int hash, string? value) =>
        (hash * 397) ^ (value is null ? 0 : StringComparer.Ordinal.GetHashCode(value));
}
