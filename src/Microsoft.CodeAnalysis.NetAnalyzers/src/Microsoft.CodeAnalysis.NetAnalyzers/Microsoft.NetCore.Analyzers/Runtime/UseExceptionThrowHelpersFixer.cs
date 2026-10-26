// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>Fixer for <see cref="UseExceptionThrowHelpers"/>.</summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class UseExceptionThrowHelpersFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
            UseExceptionThrowHelpers.UseArgumentNullExceptionThrowIfNullRuleId,
            UseExceptionThrowHelpers.UseArgumentExceptionThrowIfNullOrEmptyRuleId,
            UseExceptionThrowHelpers.UseArgumentOutOfRangeExceptionThrowIfRuleId,
            UseExceptionThrowHelpers.UseObjectDisposedExceptionThrowIfRuleId);

        public sealed override FixAllProvider GetFixAllProvider() => CustomFixAllProvider.Instance;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            SemanticModel model = await doc.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (TryGetFixInfo(doc, root, model, context.Diagnostics[0], out INamedTypeSymbol? typeSymbol, out string? methodName, out SyntaxNode? node, out SyntaxNode? arg, out SyntaxNode? other))
            {
                string title = string.Format(MicrosoftNetCoreAnalyzersResources.UseThrowHelperFix, typeSymbol.Name, methodName);
                context.RegisterCodeFix(
                    CodeAction.Create(title, equivalenceKey: title, createChangedDocument: async cancellationToken =>
                    {
                        DocumentEditor editor = await DocumentEditor.CreateAsync(doc, cancellationToken).ConfigureAwait(false);
                        ApplyFix(typeSymbol, methodName, node, arg, other, editor);
                        return editor.GetChangedDocument();
                    }),
                    context.Diagnostics);
            }
        }

        private static bool TryGetFixInfo(
            Document doc,
            SyntaxNode root,
            SemanticModel model,
            Diagnostic diagnostic,
            [NotNullWhen(true)] out INamedTypeSymbol? typeSymbol,
            [NotNullWhen(true)] out string? methodName,
            [NotNullWhen(true)] out SyntaxNode? node,
            [NotNullWhen(true)] out SyntaxNode? arg,
            [NotNullWhen(true)] out SyntaxNode? other)
        {
            typeSymbol = null;
            methodName = null;
            arg = null;
            other = null;

            node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node != null &&
                diagnostic.AdditionalLocations.Count != 0 &&
                diagnostic.AdditionalLocations[0] is Location argLocation)
            {
                arg = root.FindNode(argLocation.SourceSpan, getInnermostNodeForTie: true);
                string id = diagnostic.Id;

                if (diagnostic.AdditionalLocations.Count == 2)
                {
                    Location otherLocation = diagnostic.AdditionalLocations[1];
                    other = otherLocation == Location.None ? // None is special-cased by the analyzer to mean "this"
                        SyntaxGenerator.GetGenerator(doc).ThisExpression() :
                        root.FindNode(otherLocation.SourceSpan, getInnermostNodeForTie: true);
                }

                switch (id)
                {
                    case UseExceptionThrowHelpers.UseArgumentNullExceptionThrowIfNullRuleId:
                        typeSymbol = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemArgumentNullException);
                        methodName = "ThrowIfNull";
                        break;

                    case UseExceptionThrowHelpers.UseArgumentExceptionThrowIfNullOrEmptyRuleId:
                        typeSymbol = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemArgumentException);
                        methodName = "ThrowIfNullOrEmpty";
                        break;

                    case UseExceptionThrowHelpers.UseArgumentOutOfRangeExceptionThrowIfRuleId:
                        typeSymbol = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemArgumentOutOfRangeException);
                        diagnostic.Properties.TryGetValue(UseExceptionThrowHelpers.MethodNamePropertyKey, out methodName);
                        break;

                    case UseExceptionThrowHelpers.UseObjectDisposedExceptionThrowIfRuleId when other is not null:
                        typeSymbol = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemObjectDisposedException);
                        methodName = "ThrowIf";
                        break;
                }
            }

            return typeSymbol != null && methodName != null && arg != null;
        }

        private static void ApplyFix(
            INamedTypeSymbol typeSymbol,
            string methodName,
            SyntaxNode node,
            SyntaxNode arg,
            SyntaxNode other,
            SyntaxEditor editor)
        {
            editor.ReplaceNode(
                node,
                editor.Generator.ExpressionStatement(
                    editor.Generator.InvocationExpression(
                        editor.Generator.MemberAccessExpression(
                            editor.Generator.TypeExpressionForStaticMemberAccess(typeSymbol), methodName),
                            other is not null ? new SyntaxNode[] { arg, other } : new SyntaxNode[] { arg })).WithTriviaFrom(node));
        }

        private sealed class CustomFixAllProvider : DocumentBasedFixAllProvider
        {
            public static readonly CustomFixAllProvider Instance = new();

            protected override string GetFixAllTitle(FixAllContext fixAllContext)
                => MicrosoftNetCoreAnalyzersResources.UseThrowHelperFix;

            protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
            {
                DocumentEditor editor = await DocumentEditor.CreateAsync(document, fixAllContext.CancellationToken).ConfigureAwait(false);
                SyntaxNode root = editor.OriginalRoot;
                SemanticModel model = editor.SemanticModel;

                foreach (Diagnostic diagnostic in diagnostics)
                {
                    if (TryGetFixInfo(document, root, model, diagnostic, out INamedTypeSymbol? typeSymbol, out string? methodName, out SyntaxNode? node, out SyntaxNode? arg, out SyntaxNode? other))
                    {
                        ApplyFix(typeSymbol, methodName, node, arg, other, editor);
                    }
                }

                return editor.GetChangedDocument();
            }
        }
    }
}