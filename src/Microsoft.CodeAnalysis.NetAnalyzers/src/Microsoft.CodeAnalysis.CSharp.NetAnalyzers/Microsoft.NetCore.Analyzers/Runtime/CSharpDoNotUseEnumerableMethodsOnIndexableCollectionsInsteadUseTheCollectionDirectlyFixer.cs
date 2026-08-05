// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpDoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyFixer : DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyFixer
    {
        private protected sealed override SyntaxNode? AdjustSyntaxNode(SyntaxNode? syntaxNode)
        {
            if (syntaxNode?.Parent.IsKind(SyntaxKind.SuppressNullableWarningExpression) == true)
            {
                return syntaxNode.Parent;
            }

            return syntaxNode;
        }
    }
}
