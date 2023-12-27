// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.NetCore.Analyzers.Performance;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpPreferDictionaryTryMethodsOverContainsKeyGuardFixer : PreferDictionaryTryMethodsOverContainsKeyGuardFixer
    {
        private const string Var = "var";

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.FirstOrDefault();
            if (diagnostic is not { AdditionalLocations.Count: > 0 })
            {
                return;
            }

            Document document = context.Document;
            SyntaxNode root = await document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (root.FindNode(context.Span) is not InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax containsKeyAccess
                } containsKeyInvocation)
            {
                return;
            }

            CodeAction? action = diagnostic.Id == PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryGetValueRuleId
                ? await GetTryGetValueActionAsync(diagnostic, root, document, containsKeyAccess, containsKeyInvocation, context.CancellationToken).ConfigureAwait(false)
                : GetTryAddAction(diagnostic, root, document, containsKeyInvocation, containsKeyAccess);
            if (action is null)
            {
                return;
            }

            context.RegisterCodeFix(action, context.Diagnostics);
        }

        private static async Task<CodeAction?> GetTryGetValueActionAsync(Diagnostic diagnostic, SyntaxNode root, Document document, MemberAccessExpressionSyntax containsKeyAccess, InvocationExpressionSyntax containsKeyInvocation, CancellationToken cancellationToken)
        {
            var dictionaryAccessors = new List<SyntaxNode>();
            ExpressionStatementSyntax? addStatementNode = null;
            SyntaxNode? changedValueNode = null;
            foreach (var location in diagnostic.AdditionalLocations)
            {
                var node = root.FindNode(location.SourceSpan, getInnermostNodeForTie: true);
                switch (node)
                {
                    case ElementAccessExpressionSyntax:
                        dictionaryAccessors.Add(node);
                        break;
                    case ExpressionStatementSyntax exp:
                        if (addStatementNode != null)
                            return null;

                        addStatementNode = exp;
                        switch (addStatementNode.Expression)
                        {
                            case AssignmentExpressionSyntax assign:
                                changedValueNode = assign.Right;
                                break;
                            case InvocationExpressionSyntax invocation:
                                changedValueNode = invocation.ArgumentList.Arguments[1].Expression;
                                break;
                            default:
                                return null;
                        }

                        break;
                }
            }

            if (diagnostic.AdditionalLocations.Count != dictionaryAccessors.Count + (addStatementNode != null ? 1 : 0))
                return null;

            var model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var type = model.GetTypeInfo(dictionaryAccessors[0], cancellationToken).Type;

            return CodeAction.Create(PreferDictionaryTryGetValueCodeFixTitle, async ct =>
            {
                var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
                var generator = editor.Generator;

                // Roslyn has reducers that are run after a code action is applied, one of which will
                // simplify a TypeSyntax to `var` if the user prefers that. So we generate TypeSyntax, add
                // simplifier annotation, and then let Roslyn decide whether to keep TypeSyntax or convert it to var.
                // If the type is unknown (null) (likely in error scenario), then fallback to using var.
                TypeSyntax typeSyntax;
                if (type is not null)
                {
                    typeSyntax = (TypeSyntax)generator.TypeExpression(type);
                    if (type.IsReferenceType)
                        typeSyntax = (TypeSyntax)generator.NullableTypeExpression(typeSyntax);

                    typeSyntax = typeSyntax.WithAdditionalAnnotations(Simplifier.Annotation);
                }
                else
                {
                    typeSyntax = IdentifierName(Var);
                }

                var outArgument = (ArgumentSyntax)generator.Argument(RefKind.Out,
                    DeclarationExpression(
                        typeSyntax,
                        SingleVariableDesignation(Identifier(Value))
                    )
                );

                var tryGetValueInvocation = containsKeyInvocation
                    .ReplaceNode(containsKeyAccess.Name, IdentifierName(TryGetValue).WithTriviaFrom(containsKeyAccess.Name))
                    .AddArgumentListArguments(outArgument);
                editor.ReplaceNode(containsKeyInvocation, tryGetValueInvocation);

                var identifierName = (IdentifierNameSyntax)generator.IdentifierName(Value);
                if (addStatementNode != null)
                {
                    editor.InsertBefore(addStatementNode,
                        generator.ExpressionStatement(generator.AssignmentStatement(identifierName, changedValueNode)));
                    editor.ReplaceNode(changedValueNode!, identifierName);
                }

                foreach (var dictionaryAccess in dictionaryAccessors)
                {
                    switch (dictionaryAccess.Parent)
                    {
                        case PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PostDecrementExpression } post:
                            editor.ReplaceNode(post, generator.AssignmentStatement(dictionaryAccess,
                                PrefixUnaryExpression(SyntaxKind.PreDecrementExpression, identifierName)).
                                WithTriviaFrom(post));
                            break;
                        case PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PostIncrementExpression } post:
                            editor.ReplaceNode(post, generator.AssignmentStatement(dictionaryAccess,
                                PrefixUnaryExpression(SyntaxKind.PreIncrementExpression, identifierName)).
                                WithTriviaFrom(post));
                            break;
                        case PrefixUnaryExpressionSyntax pre:
                            editor.ReplaceNode(pre, generator.AssignmentStatement(dictionaryAccess,
                                pre.WithOperand(identifierName)).WithTriviaFrom(pre));
                            break;
                        default:
                            editor.ReplaceNode(dictionaryAccess, identifierName);
                            break;
                    }
                }

                return editor.GetChangedDocument();
            }, PreferDictionaryTryGetValueCodeFixTitle);
        }

        private static CodeAction? GetTryAddAction(Diagnostic diagnostic, SyntaxNode root, Document document, InvocationExpressionSyntax containsKeyInvocation, MemberAccessExpressionSyntax containsKeyAccess)
        {
            var dictionaryAdd = root.FindNode(diagnostic.AdditionalLocations[0].SourceSpan, getInnermostNodeForTie: true);
            if (dictionaryAdd is not InvocationExpressionSyntax dictionaryAddInvocation)
            {
                return null;
            }

            return CodeAction.Create(PreferDictionaryTryAddValueCodeFixTitle, async ct =>
            {
                var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
                var generator = editor.Generator;

                var tryAddValueAccess = generator.MemberAccessExpression(containsKeyAccess.Expression, TryAdd);
                var dictionaryAddArguments = dictionaryAddInvocation.ArgumentList.Arguments;
                var tryAddInvocation = generator.InvocationExpression(tryAddValueAccess, dictionaryAddArguments[0], dictionaryAddArguments[1]);

                var ifStatement = containsKeyInvocation.FirstAncestorOrSelf<IfStatementSyntax>();
                if (ifStatement is null)
                {
                    return editor.OriginalDocument;
                }

                if (ifStatement.Condition is PrefixUnaryExpressionSyntax unary && unary.IsKind(SyntaxKind.LogicalNotExpression))
                {
                    if (ifStatement.Statement is BlockSyntax { Statements.Count: 1 } or ExpressionStatementSyntax)
                    {
                        if (ifStatement.Else is null)
                        {
                            // d.Add() is the only statement in the if and is guarded with a !d.ContainsKey().
                            // Since there is no else-branch, we can replace the entire if-statement with a d.TryAdd() call.
                            var invocationWithTrivia = tryAddInvocation.WithTriviaFrom(ifStatement);
                            editor.ReplaceNode(ifStatement, generator.ExpressionStatement(invocationWithTrivia));
                        }
                        else
                        {
                            // d.Add() is the only statement in the if and is guarded with a !d.ContainsKey().
                            // In this case, we switch out the !d.ContainsKey() call with a !d.TryAdd() call and move the else-branch into the if.
                            editor.ReplaceNode(containsKeyInvocation, tryAddInvocation);
                            editor.ReplaceNode(ifStatement.Statement, ifStatement.Else.Statement);
                            editor.RemoveNode(ifStatement.Else, SyntaxRemoveOptions.KeepNoTrivia);
                        }
                    }
                    else
                    {
                        // d.Add() is one of many statements in the if and is guarded with a !d.ContainsKey().
                        // In this case, we switch out the !d.ContainsKey() call for a d.TryAdd() call.
                        editor.RemoveNode(dictionaryAddInvocation.Parent!, SyntaxRemoveOptions.KeepNoTrivia);
                        editor.ReplaceNode(unary, tryAddInvocation);
                    }
                }
                else if (ifStatement.Condition.IsKind(SyntaxKind.InvocationExpression) && ifStatement.Else is not null)
                {
                    var negatedTryAddInvocation = generator.LogicalNotExpression(tryAddInvocation);
                    editor.ReplaceNode(containsKeyInvocation, negatedTryAddInvocation);
                    if (ifStatement.Else.Statement is BlockSyntax { Statements.Count: 1 } or ExpressionStatementSyntax)
                    {
                        // d.Add() is the only statement the else-branch and guarded by a d.ContainsKey() call in the if.
                        // In this case we replace the d.ContainsKey() call with a !d.TryAdd() call and remove the entire else-branch.
                        editor.RemoveNode(ifStatement.Else);
                    }
                    else
                    {
                        // d.Add() is one of many statements in the else-branch and guarded by a d.ContainsKey() call in the if.
                        // In this case we replace the d.ContainsKey() call with a !d.TryAdd() call and remove the d.Add() call in the else-branch.
                        editor.RemoveNode(dictionaryAddInvocation.Parent!, SyntaxRemoveOptions.KeepNoTrivia);
                    }
                }

                return editor.GetChangedDocument();
            }, PreferDictionaryTryAddValueCodeFixTitle);
        }
    }
}