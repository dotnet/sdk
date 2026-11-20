// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    /// <summary>
    /// CA1881: Rewrites <c>Tuple.Create(a, b)</c> as the tuple literal <c>(a, b)</c>.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpPreferValueTupleOverTupleFixer : SyntaxEditorBasedCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(PreferValueTupleOverTupleAnalyzer.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            if (TryGetTupleLiteral(root, semanticModel, context.Diagnostics[0], context.CancellationToken, out _, out _))
            {
                RegisterCodeFix(
                    context,
                    MicrosoftNetCoreAnalyzersResources.PreferValueTupleOverTupleCodeFixTitle,
                    nameof(MicrosoftNetCoreAnalyzersResources.PreferValueTupleOverTupleCodeFixTitle));
            }
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (TryGetTupleLiteral(editor.OriginalRoot, semanticModel, diagnostic, cancellationToken, out var invocation, out var tupleLiteral))
            {
                editor.ReplaceNode(invocation, tupleLiteral);
            }
        }

        private static bool TryGetTupleLiteral(SyntaxNode root, SemanticModel semanticModel, Diagnostic diagnostic, CancellationToken cancellationToken, out InvocationExpressionSyntax invocation, out TupleExpressionSyntax tupleLiteral)
        {
            invocation = null!;
            tupleLiteral = null!;

            if (!diagnostic.Properties.ContainsKey(PreferValueTupleOverTupleAnalyzer.FixableKey) ||
                root.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp7 } ||
                root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not InvocationExpressionSyntax node ||
                semanticModel.GetOperation(node, cancellationToken) is not IInvocationOperation operation ||
                node.ArgumentList.Arguments.Any(argument => argument.NameColon is not null))
            {
                return false;
            }

            // An explicitly typed local would still be a Tuple.
            var parent = node.Parent;
            while (parent is ParenthesizedExpressionSyntax)
            {
                parent = parent.Parent;
            }

            if (parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Type.IsVar: false } } })
            {
                return false;
            }

            var argumentList = node.ArgumentList;
            var literal = SyntaxFactory.TupleExpression(argumentList.OpenParenToken, argumentList.Arguments, argumentList.CloseParenToken).WithTriviaFrom(node);

            // The literal takes the natural types of its elements, which differ from the Tuple's when an argument
            // is converted (Tuple.Create<long, string>(42, "hi")) or has no type of its own (default, new()).
            if (semanticModel.GetSpeculativeTypeInfo(node.SpanStart, literal, SpeculativeBindingOption.BindAsExpression).Type is not INamedTypeSymbol { IsTupleType: true } literalType ||
                !literalType.TupleElements.Select(element => element.Type).SequenceEqual(operation.TargetMethod.Parameters.Select(parameter => parameter.Type), SymbolEqualityComparer.Default))
            {
                return false;
            }

            invocation = node;
            tupleLiteral = literal;
            return true;
        }
    }
}
