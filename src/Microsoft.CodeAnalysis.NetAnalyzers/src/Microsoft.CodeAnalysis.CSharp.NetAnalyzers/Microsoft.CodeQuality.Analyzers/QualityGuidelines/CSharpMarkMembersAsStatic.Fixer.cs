// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines;

namespace Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA1822: Mark members as static
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpMarkMembersAsStaticFixer : MarkMembersAsStaticFixer
    {
        protected override IEnumerable<SyntaxNode>? GetTypeArguments(SyntaxNode node)
            => (node as GenericNameSyntax)?.TypeArgumentList.Arguments;

        protected override SyntaxNode? GetExpressionOfInvocation(SyntaxNode invocation)
            => (invocation as InvocationExpressionSyntax)?.Expression;
    }
}