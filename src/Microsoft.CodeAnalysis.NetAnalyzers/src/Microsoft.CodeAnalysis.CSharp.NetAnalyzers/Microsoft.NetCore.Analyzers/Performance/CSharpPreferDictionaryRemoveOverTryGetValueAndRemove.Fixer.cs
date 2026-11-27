// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.NetCore.Analyzers;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    /// <summary>
    /// CA1883: <inheritdoc cref="MicrosoftNetCoreAnalyzersResources.PreferDictionaryRemoveOverTryGetValueAndRemoveTitle"/>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpPreferDictionaryRemoveOverTryGetValueAndRemoveFixer : SyntaxEditorBasedCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DoNotGuardCallAnalyzer.PreferDictionaryRemoveOverTryGetValueAndRemoveRuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (!TryGetIfStatement(root, context.Diagnostics[0], out _, out _))
            {
                return;
            }

            RegisterCodeFix(
                context,
                MicrosoftNetCoreAnalyzersResources.PreferDictionaryRemoveOverTryGetValueAndRemoveCodeFixTitle,
                nameof(MicrosoftNetCoreAnalyzersResources.PreferDictionaryRemoveOverTryGetValueAndRemoveCodeFixTitle));
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            if (TryGetIfStatement(editor.OriginalRoot, diagnostic, out var ifStatement, out bool removeInElse))
            {
                // Statements after the Remove call may hold other fixes, so rewrite the if statement as those fixes left it.
                editor.ReplaceNode(ifStatement, (current, _) => Rewrite((IfStatementSyntax)current, removeInElse));
            }

            return Task.CompletedTask;
        }

        // Finds the if statement whose condition is the TryGetValue call and whose branch starts with the Remove call.
        private static bool TryGetIfStatement(SyntaxNode root, Diagnostic diagnostic, [NotNullWhen(true)] out IfStatementSyntax? ifStatement, out bool removeInElse)
        {
            removeInElse = false;
            ifStatement = null;

            if (diagnostic.AdditionalLocations.Count < 2 ||
                root.FindNode(diagnostic.AdditionalLocations[0].SourceSpan) is not IfStatementSyntax candidate ||
                root.FindNode(diagnostic.AdditionalLocations[1].SourceSpan) is not ExpressionStatementSyntax removeStatement ||
                GetConditionInvocation(candidate.Condition)?.Expression is not MemberAccessExpressionSyntax)
            {
                return false;
            }

            removeInElse = candidate.Else is not null && candidate.Else.Statement.Span.Contains(removeStatement.Span);
            if (GetFirstStatement(removeInElse ? candidate.Else!.Statement : candidate.Statement) != removeStatement)
            {
                return false;
            }

            ifStatement = candidate;
            return true;
        }

        private static SyntaxNode Rewrite(IfStatementSyntax ifStatement, bool removeInElse)
        {
            if (GetConditionInvocation(ifStatement.Condition) is not { Expression: MemberAccessExpressionSyntax memberAccess } tryGetValueInvocation)
            {
                return ifStatement;
            }

            // dictionary.TryGetValue(key, out value) becomes dictionary.Remove(key, out value)
            var removeInvocation = tryGetValueInvocation.WithExpression(
                memberAccess.WithName(SyntaxFactory.IdentifierName("Remove").WithTriviaFrom(memberAccess.Name)));
            var newIfStatement = ifStatement.WithCondition(ifStatement.Condition.ReplaceNode(tryGetValueInvocation, removeInvocation));

            var branch = removeInElse ? ifStatement.Else!.Statement : ifStatement.Statement;
            var remainingStatements = branch is BlockSyntax block ? block.Statements.RemoveAt(0) : default;

            if (remainingStatements.Count == 0)
            {
                if (removeInElse)
                {
                    return newIfStatement.WithElse(null).WithAdditionalAnnotations(Formatter.Annotation);
                }

                if (ifStatement.Else is null)
                {
                    return SyntaxFactory.ExpressionStatement(removeInvocation.WithoutTrivia())
                        .WithTriviaFrom(ifStatement)
                        .WithAdditionalAnnotations(Formatter.Annotation);
                }
            }

            var newBranch = branch is BlockSyntax originalBlock ? originalBlock.WithStatements(remainingStatements) : SyntaxFactory.Block();

            return (removeInElse
                    ? newIfStatement.WithElse(newIfStatement.Else!.WithStatement(newBranch))
                    : newIfStatement.WithStatement(newBranch))
                .WithAdditionalAnnotations(Formatter.Annotation);
        }

        // Matches the analyzer: the condition is the call itself or its negation, ignoring parentheses.
        private static InvocationExpressionSyntax? GetConditionInvocation(ExpressionSyntax condition)
        {
            var node = condition.WalkDownParentheses();

            if (node is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } not)
            {
                node = not.Operand.WalkDownParentheses();
            }

            return node as InvocationExpressionSyntax;
        }

        private static StatementSyntax? GetFirstStatement(StatementSyntax branch)
            => branch is BlockSyntax block ? block.Statements.FirstOrDefault() : branch;
    }
}
