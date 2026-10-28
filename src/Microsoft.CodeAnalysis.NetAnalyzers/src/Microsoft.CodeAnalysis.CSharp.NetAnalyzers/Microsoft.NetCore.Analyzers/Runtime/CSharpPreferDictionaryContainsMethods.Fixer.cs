// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpPreferDictionaryContainsMethodsFixer : PreferDictionaryContainsMethodsFixer
    {
        protected override string? GetPropertyName(SyntaxNode invocation)
            => GetKeysOrValuesMemberAccess(invocation)?.Name.Identifier.ValueText;

        protected override SyntaxNode? Rewrite(SyntaxNode invocation, string methodName, SyntaxGenerator generator)
        {
            if (GetKeysOrValuesMemberAccess(invocation) is not MemberAccessExpressionSyntax keysOrValuesMemberAccess)
            {
                return null;
            }

            var containsMemberAccess = generator.MemberAccessExpression(keysOrValuesMemberAccess.Expression, methodName);
            return generator.InvocationExpression(containsMemberAccess, ((InvocationExpressionSyntax)invocation).ArgumentList.Arguments);
        }

        private static MemberAccessExpressionSyntax? GetKeysOrValuesMemberAccess(SyntaxNode node)
        {
            if (node is not InvocationExpressionSyntax invocation ||
                invocation.Expression is not MemberAccessExpressionSyntax containsMemberAccess)
            {
                return null;
            }

            return containsMemberAccess.Expression.WalkDownParentheses() as MemberAccessExpressionSyntax;
        }
    }
}
