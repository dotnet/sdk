// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA2234: Pass system uri objects instead of strings
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpPassSystemUriObjectsInsteadOfStringsAnalyzer : PassSystemUriObjectsInsteadOfStringsAnalyzer
    {
        protected override SyntaxNode? GetInvocationExpression(SyntaxNode invocationNode)
        {
            var invocationExpression = invocationNode as InvocationExpressionSyntax;
            return invocationExpression?.Expression;
        }
    }
}