// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    /// <summary>
    /// CA1855: C# implementation of use Span.Clear instead of Span.Fill(default)
    /// Implements the <see cref="CodeFixProvider" />
    /// </summary>
    /// <seealso cref="UseSpanClearInsteadOfFillFixer"/>
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public sealed class CSharpUseSpanClearInsteadOfFillFixer : UseSpanClearInsteadOfFillFixer
    {
        protected override SyntaxNode? GetInvocationTarget(SyntaxNode? node)
        {
            if (node is InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax memberAccess
                })
            {
                return memberAccess.Expression;
            }

            return null;
        }
    }
}