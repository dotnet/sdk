// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public class PreferConstCharOverConstUnitStringFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(PreferConstCharOverConstUnitStringAnalyzer.RuleId);

        // Several `Append` calls can reference one const local, and the fix rewrites that local's declaration,
        // so a fix-all pass has to rewrite it once rather than once per call.
        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<HashSet<SyntaxNode>>(
                static _ => new HashSet<SyntaxNode>(),
                ApplyFixAsync);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document document = context.Document;
            CancellationToken cancellationToken = context.CancellationToken;
            SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (await TryGetFixAsync(root.FindNode(context.Span), semanticModel, cancellationToken).ConfigureAwait(false) is null)
            {
                return;
            }

            ImmutableArray<Diagnostic> diagnostics = context.Diagnostics;
            HashSet<SyntaxNode> rewritten = new();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: MicrosoftNetCoreAnalyzersResources.PreferConstCharOverConstUnitStringInStringBuilderTitle,
                    createChangedDocument: cancellationToken => SyntaxEditorFixAllProvider.ApplyFixesAsync(
                        document,
                        diagnostics,
                        (document, diagnostic, editor, cancellationToken) => ApplyFixAsync(document, diagnostic, editor, rewritten, cancellationToken),
                        cancellationToken),
                    equivalenceKey: MicrosoftNetCoreAnalyzersResources.PreferConstCharOverConstUnitStringInStringBuilderMessage),
                diagnostics);
        }

        private static async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, HashSet<SyntaxNode> rewritten, CancellationToken cancellationToken)
        {
            SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);

            if (await TryGetFixAsync(node, semanticModel, cancellationToken).ConfigureAwait(false) is not { } fix ||
                !rewritten.Add(fix.Target))
            {
                return;
            }

            (SyntaxNode target, char charValue, string? localName) = fix;

            SyntaxGenerator generator = editor.Generator;
            SyntaxNode charLiteralExpressionNode = generator.LiteralExpression(charValue);

            if (localName is null)
            {
                // Both replacements are generated from the constant value alone, so neither carries any of the
                // syntax it replaces over into the new tree and neither needs to re-read the current node.
                editor.ReplaceNode(target, charLiteralExpressionNode);
                return;
            }

            SyntaxNode charTypeNode = generator.TypeExpression(SpecialType.System_Char);
            SyntaxNode charSyntaxNode = generator.LocalDeclarationStatement(charTypeNode, localName, charLiteralExpressionNode, isConst: true);
            editor.ReplaceNode(target, charSyntaxNode.WithTriviaFrom(target));
        }

        /// <summary>
        /// Returns the node the fix replaces — the reported string literal, or the whole declaration of the
        /// const local the argument references — the character it becomes, and the local's name when it is a
        /// declaration. <see langword="null"/> when the shape is not one the fix handles.
        /// </summary>
        private static async Task<(SyntaxNode Target, char CharValue, string? LocalName)?> TryGetFixAsync(
            SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            if (semanticModel.GetOperation(node, cancellationToken) is not IArgumentOperation argumentOperation)
            {
                return null;
            }

            if (argumentOperation.Value is ILiteralOperation literalOperation)
            {
                return (literalOperation.Syntax, ((string)literalOperation.ConstantValue.Value!)[0], LocalName: null);
            }

            if (argumentOperation.Value is not ILocalReferenceOperation localReferenceOperation ||
                localReferenceOperation.Local.DeclaringSyntaxReferences.FirstOrDefault() is not SyntaxReference declaringSyntaxReference)
            {
                return null;
            }

            SyntaxNode declaringSyntax = await declaringSyntaxReference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false);

            if (semanticModel.GetOperationWalkingUpParentChain(declaringSyntax, cancellationToken) is not IVariableDeclaratorOperation variableDeclaratorOperation ||
                variableDeclaratorOperation.GetVariableInitializer() is not IVariableInitializerOperation variableInitializerOperation ||
                variableDeclaratorOperation.Parent is not IVariableDeclarationOperation { Declarators.Length: 1 } variableDeclarationOperation ||
                variableDeclarationOperation.Parent is not IVariableDeclarationGroupOperation { Declarations.Length: 1 } variableGroupDeclarationOperation)
            {
                return null;
            }

            return (variableGroupDeclarationOperation.Syntax, ((string)variableInitializerOperation.Value.ConstantValue.Value!)[0], variableDeclaratorOperation.Symbol.Name);
        }
    }
}
