// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.DotNet.Cli.Resources.Generator;

/// <summary>
///  Generates strongly typed accessors for .NET CLI string resources.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ResxSourceGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<CompilationFeatures> compilationFeatures = context.CompilationProvider
            .Select(static (compilation, _) => CompilationFeatures.Create(compilation))
            .WithComparer(CompilationFeaturesComparer.s_instance);

        IncrementalValuesProvider<ResourceInput> resourceInputs = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (pair, cancellationToken) => ResourceInput.Create(
                pair.Left,
                pair.Right,
                cancellationToken));

        IncrementalValuesProvider<(ResourceInput Left, CompilationFeatures Right)> resourcesWithCompilation =
            resourceInputs.Combine(compilationFeatures);

        IncrementalValueProvider<ImmutableArray<ResourceIdentity>> resourceIdentities = resourcesWithCompilation
            .Select(static (pair, _) => ResourceIdentity.Create(pair.Left, pair.Right.AssemblyName))
            .Collect();

        IncrementalValuesProvider<((ResourceInput Left, CompilationFeatures Right) Left, ImmutableArray<ResourceIdentity> Right)>
            generation = resourcesWithCompilation
                .Combine(resourceIdentities)
                .WithTrackingName("ResourceGeneration");

        context.RegisterSourceOutput(
            generation,
            static (productionContext, pair) => Generate(
                productionContext,
                pair.Left.Left,
                pair.Left.Right,
                pair.Right));
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A source generator must report a diagnostic instead of crashing the compiler host.")]
    private static void Generate(
        SourceProductionContext context,
        ResourceInput input,
        CompilationFeatures features,
        ImmutableArray<ResourceIdentity> identities)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (!input.IsSelected)
        {
            return;
        }

        if (ReportUnsupportedOptions(context, input))
        {
            return;
        }

        if (!ResourceGenerationOptions.TryCreate(input, features.AssemblyName, out ResourceGenerationOptions? options)
            || options is null)
        {
            Report(
                context,
                GeneratorDiagnostics.s_invalidResource,
                input,
                line: 0,
                column: 0,
                input.Path,
                "the generated class or namespace is not a valid C# identifier");

            return;
        }

        if (HasDuplicateClass(context, options, identities))
        {
            return;
        }

        try
        {
            ParsedResource? resource = ResxParser.Parse(context, options.Input);
            if (resource is null)
            {
                return;
            }

            ImmutableArray<ResourceEntry> entries = ValidateMemberNames(context, options, resource.Entries);
            bool hasLocalizedSiblings = HasLocalizedSibling(identities, options);
            string source = CSharpResourceRenderer.Render(options, entries, hasLocalizedSiblings, features);

            context.AddSource(
                options.HintName,
                SourceText.From(source, Encoding.UTF8, SourceHashAlgorithm.Sha256));
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Report(
                context,
                GeneratorDiagnostics.s_invalidResource,
                options.Input,
                line: 0,
                column: 0,
                options.Input.Path,
                exception.Message);
        }
    }

    private static bool ReportUnsupportedOptions(SourceProductionContext context, ResourceInput input)
    {
        bool reported = false;

        if (ResourceInput.IsTrue(input.OmitGetResourceString))
        {
            ReportUnsupportedOption(context, input, "OmitGetResourceString");
            reported = true;
        }

        if (ResourceInput.IsTrue(input.AsConstants))
        {
            ReportUnsupportedOption(context, input, "AsConstants");
            reported = true;
        }

        if (!string.IsNullOrWhiteSpace(input.NoWarn))
        {
            ReportUnsupportedOption(context, input, "NoWarn");
            reported = true;
        }

        return reported;
    }

    private static void ReportUnsupportedOption(
        SourceProductionContext context,
        ResourceInput input,
        string optionName)
    {
        Report(
            context,
            GeneratorDiagnostics.s_unsupportedOption,
            input,
            line: 0,
            column: 0,
            input.Path,
            optionName);
    }

    private static bool HasDuplicateClass(
        SourceProductionContext context,
        ResourceGenerationOptions options,
        ImmutableArray<ResourceIdentity> identities)
    {
        ResourceIdentity? first = null;
        int count = 0;

        foreach (ResourceIdentity identity in identities)
        {
            if (!identity.CanGenerate
                || !string.Equals(identity.ClassName, options.ClassName, StringComparison.Ordinal))
            {
                continue;
            }

            count++;
            if (first is null || StringComparer.Ordinal.Compare(identity.Path, first.Path) < 0)
            {
                first = identity;
            }
        }

        if (count < 2 || first is null)
        {
            return false;
        }

        if (!string.Equals(options.Input.Path, first.Path, StringComparison.Ordinal))
        {
            Report(
                context,
                GeneratorDiagnostics.s_duplicateClass,
                options.Input,
                line: 0,
                column: 0,
                options.Input.Path,
                options.ClassName,
                first.Path);
        }

        return true;
    }

    private static ImmutableArray<ResourceEntry> ValidateMemberNames(
        SourceProductionContext context,
        ResourceGenerationOptions options,
        ImmutableArray<ResourceEntry> entries)
    {
        HashSet<string> memberNames =
        [
            with(StringComparer.Ordinal),
            options.ClassIdentifier,
            "__ResourceCache",
            "__ResourceManagerCache",
            "CreateResourceManager",
            "Culture",
            "GetCachedResourceString",
            "GetResourceString",
            "ResourceManager",
            "s_cache",
            "s_resourceManager"
        ];

        ImmutableArray<ResourceEntry>.Builder validEntries = ImmutableArray.CreateBuilder<ResourceEntry>(entries.Length);

        foreach (ResourceEntry entry in entries)
        {
            if (!memberNames.Add(entry.Identifier))
            {
                Report(
                    context,
                    GeneratorDiagnostics.s_memberConflict,
                    options.Input,
                    entry.Line,
                    entry.Column,
                    entry.Name,
                    entry.Identifier);

                continue;
            }

            validEntries.Add(entry);
        }

        if (ResourceInput.IsTrue(options.Input.EmitFormatMethods))
        {
            foreach (ResourceEntry entry in validEntries)
            {
                if (!entry.FormatArguments.HasArguments)
                {
                    continue;
                }

                string formatMethodName = "Format" + entry.Identifier;
                if (!memberNames.Add(formatMethodName))
                {
                    Report(
                        context,
                        GeneratorDiagnostics.s_memberConflict,
                        options.Input,
                        entry.Line,
                        entry.Column,
                        entry.Name,
                        formatMethodName);

                    entry.EmitFormatMethod = false;
                }
            }
        }

        return validEntries.ToImmutable();
    }

    private static bool HasLocalizedSibling(
        ImmutableArray<ResourceIdentity> identities,
        ResourceGenerationOptions options)
    {
        foreach (ResourceIdentity identity in identities)
        {
            if (identity.Path.Equals(options.Input.Path, StringComparison.Ordinal)
                || string.IsNullOrEmpty(identity.Culture))
            {
                continue;
            }

            if (identity.ResourceFamily.Equals(options.ResourceFamily, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Reports a resource diagnostic at a source location.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="descriptor">The diagnostic descriptor to report.</param>
    /// <param name="input">The resource input associated with the diagnostic.</param>
    /// <param name="line">The zero-based source line.</param>
    /// <param name="column">The zero-based source column.</param>
    /// <param name="messageArguments">The arguments used to format the diagnostic message.</param>
    internal static void Report(
        SourceProductionContext context,
        DiagnosticDescriptor descriptor,
        ResourceInput input,
        int line,
        int column,
        params object[] messageArguments)
    {
        Location location = input.CreateLocation(line, column);
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArguments));
    }
}
