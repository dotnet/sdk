// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    /// <summary>
    /// CA1829: C# implementation of use property instead of <see cref="System.Linq.Enumerable.Count{TSource}(System.Collections.Generic.IEnumerable{TSource})"/>, when available.
    /// Implements the <see cref="CodeFixProvider" />
    /// </summary>
    /// <seealso cref="UsePropertyInsteadOfCountMethodWhenAvailableFixer"/>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpUsePropertyInsteadOfCountMethodWhenAvailableFixer : UsePropertyInsteadOfCountMethodWhenAvailableFixer
    {
        /// <summary>
        /// Gets the expression from the specified <paramref name="invocationNode" /> where to replace the invocation of the
        /// <see cref="System.Linq.Enumerable.Count{TSource}(System.Collections.Generic.IEnumerable{TSource})" /> method with a property invocation.
        /// </summary>
        /// <param name="invocationNode">The invocation node to get a fixer for.</param>
        /// <param name="memberAccessNode">The member access node for the invocation node.</param>
        /// <param name="nameNode">The name node for the invocation node.</param>
        /// <returns><see langword="true" /> if a <paramref name="memberAccessNode" /> and <paramref name="nameNode" /> were found;
        /// <see langword="false" /> otherwise.</returns>
        protected override bool TryGetExpression(
            SyntaxNode invocationNode,
            [NotNullWhen(returnValue: true)] out SyntaxNode? memberAccessNode,
            [NotNullWhen(returnValue: true)] out SyntaxNode? nameNode)
        {
            if (invocationNode is InvocationExpressionSyntax invocationExpression)
            {
                switch (invocationExpression.Expression)
                {
                    case MemberAccessExpressionSyntax memberAccessExpression:
                        memberAccessNode = invocationExpression.Expression;
                        nameNode = memberAccessExpression.Name;
                        return true;
                    case MemberBindingExpressionSyntax memberBindingExpression:
                        memberAccessNode = invocationExpression.Expression;
                        nameNode = memberBindingExpression.Name;
                        return true;
                }
            }

            memberAccessNode = default;
            nameNode = default;
            return false;
        }
    }
}
