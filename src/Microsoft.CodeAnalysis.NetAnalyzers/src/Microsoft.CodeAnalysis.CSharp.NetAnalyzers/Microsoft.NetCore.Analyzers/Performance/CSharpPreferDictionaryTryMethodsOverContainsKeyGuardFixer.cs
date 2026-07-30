// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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

            if (diagnostic.Id == PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryGetValueRuleId)
            {
                var model = await document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
                if (TryGetTryGetValueFix(diagnostic, root, model, context.CancellationToken, out _))
                {
                    RegisterCodeFix(context, PreferDictionaryTryGetValueCodeFixTitle, TryGetValueEquivalenceKey);
                }
            }
            else if (TryGetTryAddFix(diagnostic, root, out _))
            {
                RegisterCodeFix(context, PreferDictionaryTryAddValueCodeFixTitle, TryAddEquivalenceKey);
            }
        }

        protected override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, FixAllState state, CancellationToken cancellationToken)
        {
            if (diagnostic.Id == PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryGetValueRuleId)
            {
                if (state.EquivalenceKey is not null && state.EquivalenceKey != TryGetValueEquivalenceKey)
                {
                    return;
                }

                var model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                if (TryGetTryGetValueFix(diagnostic, editor.OriginalRoot, model, cancellationToken, out TryGetValueFix tryGetValueFix))
                {
                    ApplyTryGetValueFix(editor, model, state, tryGetValueFix, cancellationToken);
                }
            }
            else
            {
                if (state.EquivalenceKey is not null && state.EquivalenceKey != TryAddEquivalenceKey)
                {
                    return;
                }

                if (TryGetTryAddFix(diagnostic, editor.OriginalRoot, out TryAddFix tryAddFix))
                {
                    ApplyTryAddFix(editor, tryAddFix);
                }
            }
        }

        private static bool TryGetContainsKeyInvocation(Diagnostic diagnostic, SyntaxNode root, out InvocationExpressionSyntax containsKeyInvocation, out MemberAccessExpressionSyntax containsKeyAccess)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax access
                } invocation)
            {
                containsKeyInvocation = invocation;
                containsKeyAccess = access;

                return true;
            }

            containsKeyInvocation = null!;
            containsKeyAccess = null!;

            return false;
        }

        private static bool TryGetTryGetValueFix(Diagnostic diagnostic, SyntaxNode root, SemanticModel model, CancellationToken cancellationToken, out TryGetValueFix fix)
        {
            fix = default;

            if (!TryGetContainsKeyInvocation(diagnostic, root, out var containsKeyInvocation, out var containsKeyAccess))
            {
                return false;
            }

            var dictionaryAccessors = ImmutableArray.CreateBuilder<SyntaxNode>();
            ExpressionStatementSyntax? addStatementNode = null;
            SyntaxNode? changedValueNode = null;
            string? variableName = null;
            LocalDeclarationStatementSyntax? localDeclarationStatement = null;
            VariableDeclaratorSyntax? variableDeclarator = null;
            var additionalNodes = 0;
            SyntaxNode? typeNode = null;
            foreach (var location in diagnostic.AdditionalLocations)
            {
                var node = root.FindNode(location.SourceSpan, getInnermostNodeForTie: true);
                switch (node)
                {
                    case ElementAccessExpressionSyntax:
                        dictionaryAccessors.Add(node);
                        typeNode ??= node;
                        break;
                    case ExpressionStatementSyntax exp:
                        if (addStatementNode != null)
                            return false;

                        addStatementNode = exp;
                        additionalNodes++;
                        switch (addStatementNode.Expression)
                        {
                            case AssignmentExpressionSyntax assign:
                                changedValueNode = assign.Right;
                                break;
                            case InvocationExpressionSyntax invocation:
                                changedValueNode = invocation.ArgumentList.Arguments[1].Expression;
                                break;
                            default:
                                return false;
                        }

                        break;
                    case LocalDeclarationStatementSyntax local:
                        localDeclarationStatement = local;
                        variableName = local.Declaration.Variables[0].Identifier.ValueText;
                        additionalNodes++;
                        typeNode ??= local.Declaration.Type;
                        break;
                    case VariableDeclaratorSyntax
                    {
                        Parent: VariableDeclarationSyntax
                        {
                            Parent: LocalDeclarationStatementSyntax local
                        }
                    } declarator:
                        variableDeclarator = declarator;
                        localDeclarationStatement = local;
                        variableName = declarator.Identifier.ValueText;
                        additionalNodes++;
                        typeNode ??= local.Declaration.Type;
                        break;
                }
            }

            if (diagnostic.AdditionalLocations.Count != dictionaryAccessors.Count + additionalNodes)
                return false;

            fix = new TryGetValueFix(
                containsKeyInvocation,
                containsKeyAccess,
                dictionaryAccessors.ToImmutable(),
                addStatementNode,
                changedValueNode,
                variableName,
                localDeclarationStatement,
                variableDeclarator,
                model.GetTypeInfo(typeNode!, cancellationToken).Type);

            return true;
        }

        private static void ApplyTryGetValueFix(SyntaxEditor editor, SemanticModel model, FixAllState state, TryGetValueFix fix, CancellationToken cancellationToken)
        {
            var generator = editor.Generator;

            // Roslyn has reducers that are run after a code action is applied, one of which will
            // simplify a TypeSyntax to `var` if the user prefers that. So we generate TypeSyntax, add
            // simplifier annotation, and then let Roslyn decide whether to keep TypeSyntax or convert it to var.
            // If the type is unknown (null) (likely in error scenario), then fallback to using var.
            TypeSyntax typeSyntax;
            if (fix.Type is not null)
            {
                typeSyntax = (TypeSyntax)generator.TypeExpression(fix.Type);
                if (fix.Type.IsReferenceType)
                    typeSyntax = (TypeSyntax)generator.NullableTypeExpression(typeSyntax);

                typeSyntax = typeSyntax.WithAdditionalAnnotations(Simplifier.Annotation);
            }
            else
            {
                typeSyntax = IdentifierName(Var);
            }

            var identifierName = (IdentifierNameSyntax)(fix.VariableName is not null
                ? generator.IdentifierName(fix.VariableName)
                : generator.FirstUnusedIdentifierName(model, fix.ContainsKeyInvocation.SpanStart, Value,
                    reservedNames: state.GetReservedNames(model, fix.ContainsKeyInvocation.SpanStart, cancellationToken)));
            state.RecordIntroducedName(model, fix.ContainsKeyInvocation.SpanStart, identifierName.Identifier.ValueText, cancellationToken);

            var outArgument = (ArgumentSyntax)generator.Argument(RefKind.Out,
                DeclarationExpression(
                    typeSyntax,
                    SingleVariableDesignation(identifierName.Identifier)
                )
            );

            var tryGetValueInvocation = fix.ContainsKeyInvocation
                .ReplaceNode(fix.ContainsKeyAccess.Name, IdentifierName(TryGetValue).WithTriviaFrom(fix.ContainsKeyAccess.Name))
                .AddArgumentListArguments(outArgument);
            editor.ReplaceNode(fix.ContainsKeyInvocation, tryGetValueInvocation);

            if (fix.AddStatementNode != null)
            {
                editor.InsertBefore(fix.AddStatementNode,
                    generator.ExpressionStatement(generator.AssignmentStatement(identifierName, fix.ChangedValueNode)));
                editor.ReplaceNode(fix.ChangedValueNode!, identifierName);
            }

            foreach (var dictionaryAccess in fix.DictionaryAccessors)
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

            if (fix.LocalDeclarationStatement is not null)
            {
                if (fix.VariableDeclarator is null)
                {
                    editor.RemoveNode(fix.LocalDeclarationStatement);
                }
                else
                {
                    editor.RemoveNode(fix.VariableDeclarator);
                }
            }
        }

        private static bool TryGetTryAddFix(Diagnostic diagnostic, SyntaxNode root, out TryAddFix fix)
        {
            fix = default;

            if (!TryGetContainsKeyInvocation(diagnostic, root, out var containsKeyInvocation, out var containsKeyAccess))
            {
                return false;
            }

            var dictionaryAdd = root.FindNode(diagnostic.AdditionalLocations[0].SourceSpan, getInnermostNodeForTie: true);
            if (dictionaryAdd is not InvocationExpressionSyntax dictionaryAddInvocation)
            {
                return false;
            }

            var ifStatement = containsKeyInvocation.FirstAncestorOrSelf<IfStatementSyntax>();
            if (ifStatement is null)
            {
                return false;
            }

            fix = new TryAddFix(containsKeyInvocation, containsKeyAccess, dictionaryAddInvocation, ifStatement);

            return true;
        }

        private static void ApplyTryAddFix(SyntaxEditor editor, TryAddFix fix)
        {
            var generator = editor.Generator;

            var tryAddValueAccess = generator.MemberAccessExpression(fix.ContainsKeyAccess.Expression, TryAdd);
            var dictionaryAddArguments = fix.DictionaryAddInvocation.ArgumentList.Arguments;
            var tryAddInvocation = generator.InvocationExpression(tryAddValueAccess, dictionaryAddArguments[0], dictionaryAddArguments[1]);
            var ifStatement = fix.IfStatement;

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
                        editor.ReplaceNode(fix.ContainsKeyInvocation, tryAddInvocation);
                        editor.ReplaceNode(ifStatement.Statement, ifStatement.Else.Statement);
                        editor.RemoveNode(ifStatement.Else, SyntaxRemoveOptions.KeepNoTrivia);
                    }
                }
                else
                {
                    // d.Add() is one of many statements in the if and is guarded with a !d.ContainsKey().
                    // In this case, we switch out the !d.ContainsKey() call for a d.TryAdd() call.
                    editor.RemoveNode(fix.DictionaryAddInvocation.Parent!, SyntaxRemoveOptions.KeepNoTrivia);
                    editor.ReplaceNode(unary, tryAddInvocation);
                }
            }
            else if (ifStatement.Condition.IsKind(SyntaxKind.InvocationExpression) && ifStatement.Else is not null)
            {
                var negatedTryAddInvocation = generator.LogicalNotExpression(tryAddInvocation);
                editor.ReplaceNode(fix.ContainsKeyInvocation, negatedTryAddInvocation);
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
                    editor.RemoveNode(fix.DictionaryAddInvocation.Parent!, SyntaxRemoveOptions.KeepNoTrivia);
                }
            }
        }

        private readonly struct TryGetValueFix
        {
            public TryGetValueFix(
                InvocationExpressionSyntax containsKeyInvocation,
                MemberAccessExpressionSyntax containsKeyAccess,
                ImmutableArray<SyntaxNode> dictionaryAccessors,
                ExpressionStatementSyntax? addStatementNode,
                SyntaxNode? changedValueNode,
                string? variableName,
                LocalDeclarationStatementSyntax? localDeclarationStatement,
                VariableDeclaratorSyntax? variableDeclarator,
                ITypeSymbol? type)
            {
                ContainsKeyInvocation = containsKeyInvocation;
                ContainsKeyAccess = containsKeyAccess;
                DictionaryAccessors = dictionaryAccessors;
                AddStatementNode = addStatementNode;
                ChangedValueNode = changedValueNode;
                VariableName = variableName;
                LocalDeclarationStatement = localDeclarationStatement;
                VariableDeclarator = variableDeclarator;
                Type = type;
            }

            public InvocationExpressionSyntax ContainsKeyInvocation { get; }

            public MemberAccessExpressionSyntax ContainsKeyAccess { get; }

            public ImmutableArray<SyntaxNode> DictionaryAccessors { get; }

            public ExpressionStatementSyntax? AddStatementNode { get; }

            public SyntaxNode? ChangedValueNode { get; }

            /// <summary>
            /// The name of the local the value is already read into, or <see langword="null"/> when the fix
            /// has to introduce one.
            /// </summary>
            public string? VariableName { get; }

            public LocalDeclarationStatementSyntax? LocalDeclarationStatement { get; }

            public VariableDeclaratorSyntax? VariableDeclarator { get; }

            public ITypeSymbol? Type { get; }
        }

        private readonly struct TryAddFix
        {
            public TryAddFix(
                InvocationExpressionSyntax containsKeyInvocation,
                MemberAccessExpressionSyntax containsKeyAccess,
                InvocationExpressionSyntax dictionaryAddInvocation,
                IfStatementSyntax ifStatement)
            {
                ContainsKeyInvocation = containsKeyInvocation;
                ContainsKeyAccess = containsKeyAccess;
                DictionaryAddInvocation = dictionaryAddInvocation;
                IfStatement = ifStatement;
            }

            public InvocationExpressionSyntax ContainsKeyInvocation { get; }

            public MemberAccessExpressionSyntax ContainsKeyAccess { get; }

            public InvocationExpressionSyntax DictionaryAddInvocation { get; }

            public IfStatementSyntax IfStatement { get; }
        }
    }
}