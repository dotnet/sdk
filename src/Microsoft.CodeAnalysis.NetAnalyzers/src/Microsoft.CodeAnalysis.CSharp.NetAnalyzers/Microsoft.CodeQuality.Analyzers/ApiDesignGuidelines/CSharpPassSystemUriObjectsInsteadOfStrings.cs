// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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