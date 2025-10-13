// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.Performance
{
    /// <summary>
    /// CA1876: Use ReadOnlySpan&lt;T&gt; or ReadOnlyMemory&lt;T&gt; instead of Span&lt;T&gt; or Memory&lt;T&gt;
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferReadOnlySpanOverSpanFixer))]
    [Shared]
    public sealed class PreferReadOnlySpanOverSpanFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(PreferReadOnlySpanOverSpanAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);
            var semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics[0];
            
            // Get the parameter symbol
            var parameterSymbol = semanticModel.GetDeclaredSymbol(node, context.CancellationToken) as IParameterSymbol;
            if (parameterSymbol == null)
            {
                return;
            }

            // Determine the target type name from the diagnostic properties or compute it
            var targetTypeName = GetReadOnlyTypeName(parameterSymbol.Type);
            if (targetTypeName == null)
            {
                return;
            }

            var title = string.Format(MicrosoftNetCoreAnalyzersResources.PreferReadOnlySpanOverSpanCodeFixTitle, targetTypeName);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: c => ChangeParameterTypeAsync(context.Document, node, c),
                    equivalenceKey: title),
                diagnostic);
        }

        private static string? GetReadOnlyTypeName(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is not INamedTypeSymbol namedType)
            {
                return null;
            }

            var typeName = namedType.OriginalDefinition.Name;
            if (typeName is "Span" or "Memory")
            {
                return $"ReadOnly{typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}";
            }

            return null;
        }

        private static async Task<Document> ChangeParameterTypeAsync(
            Document document,
            SyntaxNode node,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var generator = editor.Generator;
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            // Get the parameter symbol to construct the correct type
            var parameterSymbol = semanticModel.GetDeclaredSymbol(node, cancellationToken) as IParameterSymbol;
            if (parameterSymbol?.Type is not INamedTypeSymbol namedType || namedType.TypeArguments.Length != 1)
            {
                return document;
            }

            // Get the compilation to find the readonly types
            var compilation = semanticModel.Compilation;
            var typeName = namedType.OriginalDefinition.Name;
            INamedTypeSymbol? readOnlyType = null;

            if (typeName == "Span")
            {
                readOnlyType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1);
            }
            else if (typeName == "Memory")
            {
                readOnlyType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlyMemory1);
            }

            if (readOnlyType == null)
            {
                return document;
            }

            // Construct the generic type with the same type argument
            var newType = readOnlyType.Construct(namedType.TypeArguments[0]);
            var newTypeNode = generator.TypeExpression(newType);

            // Replace the parameter's type
            editor.ReplaceNode(node, (currentNode, gen) =>
            {
                return gen.WithType(currentNode, newTypeNode);
            });

            return editor.GetChangedDocument();
        }
    }
}
