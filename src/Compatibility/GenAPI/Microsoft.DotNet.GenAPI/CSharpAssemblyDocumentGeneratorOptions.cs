// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.GenAPI;

/// <summary>
/// Options for generating C# assembly documents with <see cref="CSharpAssemblyDocumentGenerator" />, allowing customization of various aspects of the generation process.
/// </summary>
public sealed class CSharpAssemblyDocumentGeneratorOptions
{
    public CSharpAssemblyDocumentGeneratorOptions(IAssemblySymbolLoader loader, ISymbolFilter symbolFilter, ISymbolFilter attributeSymbolFilter)
    {
        Loader = loader;
        SymbolFilter = symbolFilter;
        AttributeSymbolFilter = attributeSymbolFilter;
    }

    public IAssemblySymbolLoader Loader { get; set; }
    public ISymbolFilter SymbolFilter { get; set; }
    public ISymbolFilter AttributeSymbolFilter { get; set; }
    public bool HideImplicitDefaultConstructors { get; set; }
    public bool IncludeAssemblyAttributes { get; set; }
    public bool ShouldFormat { get; set; }
    public bool ShouldReduce { get; set; }
    public IEnumerable<KeyValuePair<string, ReportDiagnostic>>? DiagnosticOptions { get; set; }
    public IEnumerable<MetadataReference>? MetadataReferences { get; set; }
    public List<CSharpSyntaxRewriter> SyntaxRewriters { get; set; } = [];
    public List<SyntaxAnnotation> AdditionalAnnotations { get; set; } = [Formatter.Annotation, Simplifier.Annotation];
}
