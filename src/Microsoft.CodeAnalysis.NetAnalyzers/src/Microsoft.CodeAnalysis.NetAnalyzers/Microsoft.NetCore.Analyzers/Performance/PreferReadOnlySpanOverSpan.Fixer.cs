// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Performance
{
    /// <summary>
    /// CA1517: Use ReadOnlySpan&lt;T&gt; or ReadOnlyMemory&lt;T&gt; instead of Span&lt;T&gt; or Memory&lt;T&gt;
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferReadOnlySpanOverSpanFixer))]
    [Shared]
    public sealed class PreferReadOnlySpanOverSpanFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(PreferReadOnlySpanOverSpanAnalyzer.RuleId);

        // The title names the target type, so one document can offer several distinct actions and a
        // fix-all has to apply only the one that was invoked.
        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(
                fixAllContext => fixAllContext.CodeActionEquivalenceKey,
                async (document, diagnostic, editor, equivalenceKey, cancellationToken) =>
                {
                    var node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
                    var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

                    if (semanticModel.GetDeclaredSymbol(node, cancellationToken) is IParameterSymbol parameterSymbol &&
                        GetReadOnlyTypeName(parameterSymbol.Type) is { } targetTypeName &&
                        (equivalenceKey is null || GetTitle(targetTypeName) == equivalenceKey))
                    {
                        ChangeParameterType(editor, semanticModel.Compilation, node, (INamedTypeSymbol)parameterSymbol.Type);
                    }
                });

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);
            var semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            if (semanticModel.GetDeclaredSymbol(node, context.CancellationToken) is IParameterSymbol parameterSymbol &&
                GetReadOnlyTypeName(parameterSymbol.Type) is { } targetTypeName)
            {
                var title = GetTitle(targetTypeName);

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: title,
                        createChangedDocument: c => ChangeParameterTypeAsync(context.Document, node, c),
                        equivalenceKey: title),
                    context.Diagnostics[0]);
            }
        }

        private static string GetTitle(string targetTypeName)
            => string.Format(MicrosoftNetCoreAnalyzersResources.PreferReadOnlySpanOverSpanCodeFixTitle, targetTypeName);

        private static string? GetReadOnlyTypeName(ITypeSymbol typeSymbol) =>
            typeSymbol is INamedTypeSymbol namedType && namedType.OriginalDefinition.Name is "Span" or "Memory" ?
                $"ReadOnly{typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}" :
                null;

        private static void ChangeParameterType(SyntaxEditor editor, Compilation compilation, SyntaxNode node, INamedTypeSymbol parameterType)
        {
            if (parameterType.TypeArguments.Length != 1)
            {
                return;
            }

            var typeName = parameterType.OriginalDefinition.Name;

            INamedTypeSymbol? readOnlyType =
                typeName is "Span" ? compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1) :
                typeName is "Memory" ? compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlyMemory1) :
                null;

            if (readOnlyType is null)
            {
                return;
            }

            // Construct the generic type with the same type argument
            var newTypeNode = editor.Generator.TypeExpression(readOnlyType.Construct(parameterType.TypeArguments[0]));

            // Replace the parameter's type
            editor.ReplaceNode(node, (currentNode, gen) => gen.WithType(currentNode, newTypeNode));
        }

        private static async Task<Document> ChangeParameterTypeAsync(
            Document document,
            SyntaxNode node,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            // Get the parameter symbol to construct the correct type
            if (semanticModel.GetDeclaredSymbol(node, cancellationToken) is IParameterSymbol { Type: INamedTypeSymbol namedType })
            {
                ChangeParameterType(editor, semanticModel.Compilation, node, namedType);
            }

            return editor.GetChangedDocument();
        }
    }
}
