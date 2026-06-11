// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.NetCore.Analyzers.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CSharpUseOrdinalStringComparisonAnalyzer : UseOrdinalStringComparisonAnalyzer
    {
        protected override Location GetMethodNameLocation(SyntaxNode invocationNode)
        {
            Debug.Assert(invocationNode.IsKind(SyntaxKind.InvocationExpression));

            var invocation = (InvocationExpressionSyntax)invocationNode;
            if (invocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                return ((MemberAccessExpressionSyntax)invocation.Expression).Name.GetLocation();
            }
            else if (invocation.Expression.IsKind(SyntaxKind.ConditionalAccessExpression))
            {
                return ((ConditionalAccessExpressionSyntax)invocation.Expression).WhenNotNull.GetLocation();
            }

            return invocation.GetLocation();
        }
    }
}
