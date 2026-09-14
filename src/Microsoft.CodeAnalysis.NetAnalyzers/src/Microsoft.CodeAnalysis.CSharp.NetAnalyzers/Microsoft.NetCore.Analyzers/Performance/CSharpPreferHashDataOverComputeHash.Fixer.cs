// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpPreferHashDataOverComputeHashFixer : PreferHashDataOverComputeHashFixer
    {
        private static readonly CSharpPreferHashDataOverComputeHashFixAllProvider s_fixAllProvider = new();
        private static readonly CSharpPreferHashDataOverComputeHashFixHelper s_helper = new();

        public sealed override FixAllProvider GetFixAllProvider() => s_fixAllProvider;

        protected override PreferHashDataOverComputeHashFixHelper Helper => s_helper;

        private sealed class CSharpPreferHashDataOverComputeHashFixAllProvider : PreferHashDataOverComputeHashFixAllProvider
        {
            protected override PreferHashDataOverComputeHashFixHelper Helper => s_helper;
        }

        private sealed class CSharpPreferHashDataOverComputeHashFixHelper : PreferHashDataOverComputeHashFixHelper
        {
            protected override SyntaxNode GetHashDataSyntaxNode(PreferHashDataOverComputeHashAnalyzer.ComputeType computeType, string? namespacePrefix, string hashTypeName, SyntaxNode computeHashNode)
            {
                string identifier = hashTypeName;
                if (namespacePrefix is not null)
                {
                    identifier = $"{namespacePrefix}.{identifier}";
                }

                var argumentList = ((InvocationExpressionSyntax)computeHashNode).ArgumentList;
                switch (computeType)
                {
                    // hashTypeName.HashData(buffer)
                    case PreferHashDataOverComputeHashAnalyzer.ComputeType.ComputeHash:
                        {
                            var hashData = SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.ParseExpression(identifier),
                                SyntaxFactory.IdentifierName(PreferHashDataOverComputeHashAnalyzer.HashDataMethodName));
                            var arg = argumentList.Arguments[0];
                            if (arg.NameColon is not null)
                            {
                                arg = arg.WithNameColon(arg.NameColon.WithName(SyntaxFactory.IdentifierName("source")));
                            }

                            var args = SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(arg));
                            return SyntaxFactory.InvocationExpression(hashData, args);
                        }
                    // hashTypeName.HashData(buffer.AsSpan(start, end))
                    case PreferHashDataOverComputeHashAnalyzer.ComputeType.ComputeHashSection:
                        {
                            var list = argumentList.Arguments.ToList();
                            var firstArg = list.Find(a => a.NameColon is null || a.NameColon.Name.Identifier.Text.Equals("buffer", StringComparison.Ordinal));
                            list.Remove(firstArg);
                            var secondArgIndex = list.FindIndex(a => a.NameColon is null || a.NameColon.Name.Identifier.Text.Equals("offset", StringComparison.Ordinal));
                            var thirdArgIndex = (secondArgIndex == 0) ? 1 : 0; // second and third can only be 0 or 1
                            var secondArg = list[secondArgIndex];
                            if (secondArg.NameColon is not null)
                            {
                                list[secondArgIndex] = secondArg.WithNameColon(SyntaxFactory.NameColon(SyntaxFactory.IdentifierName("start")));
                            }

                            var thirdArg = list[thirdArgIndex];
                            if (thirdArg.NameColon is not null)
                            {
                                list[thirdArgIndex] = thirdArg.WithNameColon(SyntaxFactory.NameColon(SyntaxFactory.IdentifierName("length")));
                            }

                            var asSpan = SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                firstArg.Expression,
                                SyntaxFactory.IdentifierName("AsSpan"));
                            var spanArgs = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(list));
                            var asSpanInvoked = SyntaxFactory.InvocationExpression(asSpan, spanArgs);
                            var hashData = SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.ParseExpression(identifier),
                                SyntaxFactory.IdentifierName(PreferHashDataOverComputeHashAnalyzer.HashDataMethodName));
                            var arg = SyntaxFactory.Argument(asSpanInvoked);
                            if (firstArg.NameColon is not null)
                            {
                                arg = arg.WithNameColon(SyntaxFactory.NameColon(SyntaxFactory.IdentifierName("source")));
                            }

                            var args = SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(arg));
                            return SyntaxFactory.InvocationExpression(hashData, args);
                        }
                    // hashTypeName.TryHashData(rosSpan, span, write)
                    case PreferHashDataOverComputeHashAnalyzer.ComputeType.TryComputeHash:
                        {
                            // method has same parameter names
                            var hashData = SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.ParseExpression(identifier),
                                SyntaxFactory.IdentifierName(PreferHashDataOverComputeHashAnalyzer.TryHashDataMethodName));
                            return SyntaxFactory.InvocationExpression(hashData, argumentList);
                        }
                    default:
                        Debug.Fail("there is only 3 type of ComputeHash");
                        throw new InvalidOperationException("there is only 3 type of ComputeHash");
                }
            }

            protected override SyntaxNode FixHashCreateNode(SyntaxNode root, SyntaxNode createNode)
            {
                var currentCreateNode = root.GetCurrentNode(createNode)!;
                switch (currentCreateNode.Parent)
                {
                    case { Parent: UsingStatementSyntax usingStatement } when usingStatement.Declaration?.Variables.Count == 1:
                        {
                            root = MoveStatementsOutOfUsingStatementWithFormatting(root, usingStatement);
                            break;
                        }
                    case { Parent: UsingStatementSyntax }:
                        {
                            root = RemoveNodeWithFormatting(root, currentCreateNode);
                            break;
                        }
                    case { Parent: LocalDeclarationStatementSyntax localDeclarationStatementSyntax }:
                        {
                            root = RemoveNodeWithFormatting(root, localDeclarationStatementSyntax);
                            break;
                        }
                    case VariableDeclaratorSyntax variableDeclaratorSyntax:
                        {
                            root = RemoveNodeWithFormatting(root, variableDeclaratorSyntax);
                            break;
                        }
                }

                return root;
            }

            private SyntaxNode MoveStatementsOutOfUsingStatementWithFormatting(SyntaxNode root, UsingStatementSyntax usingStatement)
            {
                var block = (BlockSyntax)usingStatement.Statement;
                var statements = block.Statements
                    .Select((s, i) =>
                    {
                        var statement = s;
                        if (i == 0)
                        {
                            var newTrivia = new SyntaxTriviaList();
                            newTrivia = AddRangeIfInteresting(newTrivia, usingStatement.GetLeadingTrivia());
                            newTrivia = AddRangeIfInteresting(newTrivia, usingStatement.CloseParenToken.LeadingTrivia);
                            newTrivia = AddRangeIfInteresting(newTrivia, usingStatement.CloseParenToken.TrailingTrivia);
                            newTrivia = AddRangeIfInteresting(newTrivia, block.OpenBraceToken.LeadingTrivia);
                            newTrivia = AddRangeIfInteresting(newTrivia, block.OpenBraceToken.TrailingTrivia);
                            newTrivia = newTrivia.AddRange(statement.GetLeadingTrivia());
                            statement = statement.WithLeadingTrivia(newTrivia);
                        }

                        if (i == block.Statements.Count - 1)
                        {
                            var newTrivia = statement.GetTrailingTrivia();
                            newTrivia = AddRangeIfInteresting(newTrivia, block.CloseBraceToken.LeadingTrivia);
                            newTrivia = AddRangeIfInteresting(newTrivia, block.CloseBraceToken.TrailingTrivia);
                            newTrivia = AddRangeIfInteresting(newTrivia, usingStatement.GetTrailingTrivia());
                            statement = statement.WithTrailingTrivia(newTrivia);
                        }

                        return statement;
                    });

                var parent = usingStatement.Parent!;
                if (parent is GlobalStatementSyntax target)
                {
                    parent = parent.Parent!;
                    parent = parent.TrackNodes(target);
                    parent = parent.InsertNodesBefore(parent.GetCurrentNode(target)!, statements.Select(SyntaxFactory.GlobalStatement));
                    parent = parent.RemoveNode(parent.GetCurrentNode(target)!, SyntaxRemoveOptions.KeepNoTrivia)!
                        .WithAdditionalAnnotations(Formatter.Annotation);
                    root = parent;
                }
                else
                {
                    root = root.TrackNodes(parent);
                    var newParent = parent.TrackNodes(usingStatement);
                    newParent = newParent.InsertNodesBefore(newParent.GetCurrentNode(usingStatement)!, statements);
                    newParent = newParent.RemoveNode(newParent.GetCurrentNode(usingStatement)!, SyntaxRemoveOptions.KeepNoTrivia)!
                        .WithAdditionalAnnotations(Formatter.Annotation);
                    root = root.ReplaceNode(root.GetCurrentNode(parent)!, newParent);
                }

                return root;
            }

            protected override bool IsInterestingTrivia(SyntaxTriviaList triviaList)
            {
                return triviaList.Any(t => !t.IsKind(SyntaxKind.WhitespaceTrivia) && !t.IsKind(SyntaxKind.EndOfLineTrivia));
            }
            protected override string? GetQualifiedPrefixNamespaces(SyntaxNode computeHashNode, SyntaxNode? createNode)
            {
                var invocationNode = (InvocationExpressionSyntax)computeHashNode;
                string? ns = null;
                if (createNode is not null)
                {
                    var initliazerValue = ((VariableDeclaratorSyntax)createNode).Initializer?.Value;
                    if (initliazerValue is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: MemberAccessExpressionSyntax originalType } })
                    {
                        ns = originalType.Expression.ToFullString();
                    }
                    else if (initliazerValue is ObjectCreationExpressionSyntax { Type: QualifiedNameSyntax { Left: QualifiedNameSyntax qualifiedNamespaceSyntax } })
                    {
                        ns = qualifiedNamespaceSyntax.ToFullString();
                    }
                }
                else if (invocationNode.Expression is MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: MemberAccessExpressionSyntax originalType } } })
                {
                    // System.Security.Cryptography.SHA1.Create().ComputeHash(buffer)
                    // .ComputeHash(buffer) InvocationExpressionSyntax, MemberAccessExpressionSyntax
                    // .Create() InvocationExpressionSyntax, MemberAccessExpressionSyntax
                    ns = originalType.Expression.ToFullString();
                }
                else if (invocationNode.Expression is MemberAccessExpressionSyntax { Expression: ObjectCreationExpressionSyntax { Type: QualifiedNameSyntax { Left: QualifiedNameSyntax qualifiedNamespaceSyntax } } })
                {
                    // new System.Security.Cryptography.SHA1Managed().ComputeHash(buffer)
                    // .ComputeHash(buffer) InvocationExpressionSyntax, MemberAccessExpressionSyntax
                    // new System.Security.Cryptography.SHA1Managed() ObjectCreationExpressionSyntax
                    ns = qualifiedNamespaceSyntax.ToFullString();
                }

                return ns;
            }
        }
    }
}
