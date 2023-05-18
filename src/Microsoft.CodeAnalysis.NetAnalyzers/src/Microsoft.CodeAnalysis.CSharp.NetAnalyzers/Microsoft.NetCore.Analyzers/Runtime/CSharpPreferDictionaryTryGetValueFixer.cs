// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.NetCore.Analyzers.Runtime;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpPreferDictionaryTryGetValueFixer : PreferDictionaryTryGetValueFixer
    {
        private const string Var = "var";

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.FirstOrDefault();
            if (diagnostic is not { AdditionalLocations.Count: > 0 })
                return;

            Document document = context.Document;
            SyntaxNode root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (root.FindNode(context.Span) is not InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax containsKeyAccess
                } containsKeyInvocation)
            {
                return;
            }

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
                            return;

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
                                return;
                        }

                        break;
                }
            }

            if (diagnostic.AdditionalLocations.Count != dictionaryAccessors.Count + (addStatementNode != null ? 1 : 0))
                return;

            var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var type = model.GetTypeInfo(dictionaryAccessors[0], context.CancellationToken).Type;

            var action = CodeAction.Create(PreferDictionaryTryGetValueCodeFixTitle, async ct =>
            {
                var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
                var generator = editor.Generator;

                var tryGetValueAccess = generator.MemberAccessExpression(containsKeyAccess.Expression, TryGetValue);
                var keyArgument = containsKeyInvocation.ArgumentList.Arguments.FirstOrDefault();

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

                var outArgument = generator.Argument(RefKind.Out,
                    DeclarationExpression(
                        typeSyntax,
                        SingleVariableDesignation(Identifier(Value))
                        )
                    );
                var tryGetValueInvocation = generator.InvocationExpression(tryGetValueAccess, keyArgument, outArgument);
                editor.ReplaceNode(containsKeyInvocation, tryGetValueInvocation);

                var identifierName = (IdentifierNameSyntax)generator.IdentifierName(Value);
                if (addStatementNode != null)
                {
                    editor.InsertBefore(addStatementNode,
                        generator.ExpressionStatement(generator.AssignmentStatement(identifierName, changedValueNode)));
                    editor.ReplaceNode(changedValueNode, identifierName);
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

            context.RegisterCodeFix(action, context.Diagnostics);
        }
    }
}