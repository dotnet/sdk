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
            protected override SyntaxNode GetHashDataSyntaxNode(PreferHashDataOverComputeHashAnalyzer.ComputeType computeType, string hashTypeName, SyntaxNode computeHashNode)
            {
                var argumentList = ((InvocationExpressionSyntax)computeHashNode).ArgumentList;
                switch (computeType)
                {
                    // hashTypeName.HashData(buffer)
                    case PreferHashDataOverComputeHashAnalyzer.ComputeType.ComputeHash:
                        {
                            var hashData = SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName(hashTypeName),
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
                                SyntaxFactory.IdentifierName(hashTypeName),
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
                                SyntaxFactory.IdentifierName(hashTypeName),
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
                var currentCreateNode = root.GetCurrentNode(createNode);
                switch (currentCreateNode.Parent)
                {
                    case { Parent: UsingStatementSyntax usingStatement } when usingStatement.Declaration.Variables.Count == 1:
                        {
                            var statements = usingStatement.Statement
                                .ChildNodes()
                                .Select(s => s.WithAdditionalAnnotations(Formatter.Annotation));
                            root = root.TrackNodes(usingStatement);
                            root = root.InsertNodesBefore(root.GetCurrentNode(usingStatement), statements);
                            root = root.RemoveNode(root.GetCurrentNode(usingStatement), SyntaxRemoveOptions.KeepNoTrivia);
                            break;
                        }
                    case { Parent: UsingStatementSyntax usingStatement }:
                        {
                            root = root.RemoveNode(currentCreateNode, SyntaxRemoveOptions.KeepNoTrivia);
                            break;
                        }
                    case { Parent: LocalDeclarationStatementSyntax localDeclarationStatementSyntax }:
                        {
                            root = root.RemoveNode(localDeclarationStatementSyntax, SyntaxRemoveOptions.KeepNoTrivia);
                            break;
                        }
                    case VariableDeclaratorSyntax variableDeclaratorSyntax:
                        {
                            root = root.RemoveNode(variableDeclaratorSyntax, SyntaxRemoveOptions.KeepNoTrivia);
                            break;
                        }
                }
                return root;
            }
        }
    }
}
