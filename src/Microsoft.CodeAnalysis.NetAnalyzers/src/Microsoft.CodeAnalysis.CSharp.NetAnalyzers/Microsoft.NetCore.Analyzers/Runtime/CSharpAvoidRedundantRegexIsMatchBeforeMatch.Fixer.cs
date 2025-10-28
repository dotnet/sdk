// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    /// <summary>
    /// CA2027: <inheritdoc cref="NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.AvoidRedundantRegexIsMatchBeforeMatchMessage"/>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpAvoidRedundantRegexIsMatchBeforeMatchFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(AvoidRedundantRegexIsMatchBeforeMatch.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            if (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false) is not { } root ||
                await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false) is not { } semanticModel)
            {
                return;
            }

            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);

            if (node is not InvocationExpressionSyntax isMatchInvocation)
            {
                return;
            }

            // Find the if statement that contains this IsMatch call
            var ifStatement = node.FirstAncestorOrSelf<IfStatementSyntax>();
            if (ifStatement is null)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.AvoidRedundantRegexIsMatchBeforeMatchFix,
                    ct => RemoveRedundantIsMatchAsync(context.Document, root, ifStatement, isMatchInvocation, semanticModel, ct),
                    equivalenceKey: NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.AvoidRedundantRegexIsMatchBeforeMatchFix),
                context.Diagnostics[0]);
        }

        private static async Task<Document> RemoveRedundantIsMatchAsync(
            Document document,
            SyntaxNode root,
            IfStatementSyntax ifStatement,
            InvocationExpressionSyntax isMatchInvocation,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            // Find the Match call in the body and use it to replace the condition
            var matchCall = FindMatchCallInBlock(ifStatement.Statement, isMatchInvocation, semanticModel);
            
            if (matchCall is not null)
            {
                // Create the new condition: Regex.Match(...) is { Success: true } variableName
                var matchDeclaration = matchCall.Parent?.Parent as LocalDeclarationStatementSyntax;
                
                if (matchDeclaration is not null)
                {
                    var variableDeclarator = matchDeclaration.Declaration.Variables.First();
                    var variableName = variableDeclarator.Identifier.Text;

                    // Create pattern: is { Success: true }
                    var successPattern = SyntaxFactory.RecursivePattern()
                        .WithPropertyPatternClause(
                            SyntaxFactory.PropertyPatternClause(
                                SyntaxFactory.SingletonSeparatedList<SubpatternSyntax>(
                                    SyntaxFactory.Subpattern(
                                        SyntaxFactory.NameColon("Success"),
                                        SyntaxFactory.ConstantPattern(
                                            SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression))))))
                        .WithDesignation(SyntaxFactory.SingleVariableDesignation(SyntaxFactory.Identifier(variableName)));

                    var newCondition = SyntaxFactory.IsPatternExpression(
                        matchCall,
                        successPattern);

                    // Remove the Match declaration from the body
                    var newBody = ifStatement.Statement;
                    if (ifStatement.Statement is BlockSyntax block)
                    {
                        var statements = block.Statements.Where(s => s != matchDeclaration);
                        newBody = block.WithStatements(SyntaxFactory.List(statements));
                    }

                    // Create new if statement
                    var newIfStatement = ifStatement
                        .WithCondition(newCondition.WithTriviaFrom(ifStatement.Condition))
                        .WithStatement(newBody)
                        .WithAdditionalAnnotations(Formatter.Annotation);

                    editor.ReplaceNode(ifStatement, newIfStatement);
                }
            }

            return editor.GetChangedDocument();
        }

        private static InvocationExpressionSyntax? FindMatchCallInBlock(StatementSyntax statement, InvocationExpressionSyntax isMatchInvocation, SemanticModel semanticModel)
        {
            if (statement is not BlockSyntax block)
            {
                return null;
            }

            // Look for a Match call with the same arguments as IsMatch
            foreach (var stmt in block.Statements)
            {
                if (stmt is LocalDeclarationStatementSyntax localDecl)
                {
                    var variable = localDecl.Declaration.Variables.FirstOrDefault();
                    if (variable?.Initializer?.Value is InvocationExpressionSyntax invocation)
                    {
                        if (IsMatchingMatchCall(invocation, isMatchInvocation, semanticModel))
                        {
                            return invocation;
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsMatchingMatchCall(InvocationExpressionSyntax matchInvocation, InvocationExpressionSyntax isMatchInvocation, SemanticModel semanticModel)
        {
            // Check if both calls are to Regex.Match and Regex.IsMatch with matching arguments
            if (matchInvocation.Expression is not MemberAccessExpressionSyntax matchMember ||
                isMatchInvocation.Expression is not MemberAccessExpressionSyntax isMatchMember)
            {
                return false;
            }

            // Check method names
            if (matchMember.Name.Identifier.Text != "Match" ||
                isMatchMember.Name.Identifier.Text != "IsMatch")
            {
                return false;
            }

            // Check if they have the same number of arguments
            if (matchInvocation.ArgumentList.Arguments.Count != isMatchInvocation.ArgumentList.Arguments.Count)
            {
                return false;
            }

            // For a simple check, compare argument expressions syntactically
            for (int i = 0; i < matchInvocation.ArgumentList.Arguments.Count; i++)
            {
                var matchArg = matchInvocation.ArgumentList.Arguments[i].Expression.ToString();
                var isMatchArg = isMatchInvocation.ArgumentList.Arguments[i].Expression.ToString();
                if (matchArg != isMatchArg)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
