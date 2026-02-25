// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CSharpSpecifyCultureForToLowerAndToUpperAnalyzer : SpecifyCultureForToLowerAndToUpperAnalyzer
    {
        protected override Location GetMethodNameLocation(SyntaxNode node)
        {
            Debug.Assert(node.IsKind(SyntaxKind.InvocationExpression));

            var invocation = (InvocationExpressionSyntax)node;
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                return memberAccess.Name.GetLocation();
            }
            else if (invocation.Expression is MemberBindingExpressionSyntax memberBinding)
            {
                return memberBinding.Name.GetLocation();
            }

            return invocation.GetLocation();
        }
    }
}
