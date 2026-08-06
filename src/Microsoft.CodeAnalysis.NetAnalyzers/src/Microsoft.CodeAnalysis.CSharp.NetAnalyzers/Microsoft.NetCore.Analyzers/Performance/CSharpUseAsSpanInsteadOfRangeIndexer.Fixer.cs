// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    /// <summary>
    /// CA1831, CA1832, CA1833: Use AsSpan or AsMemory instead of Range-based indexers when appropriate.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpUseAsSpanInsteadOfRangeIndexerFixer : UseAsSpanInsteadOfRangeIndexerFixer
    {
        protected override bool TrySplitExpression(
            SyntaxNode node,
            out SyntaxNode toReplace,
            [NotNullWhen(true)] out SyntaxNode? target,
            [NotNullWhen(true)] out IEnumerable<SyntaxNode>? arguments)
        {
            if (node is ArgumentSyntax arg)
            {
                node = arg.Expression;
            }

            toReplace = node;

            if (node is ElementAccessExpressionSyntax elementAccess)
            {
                target = elementAccess.Expression;
                arguments = elementAccess.ArgumentList.Arguments;
                return true;
            }

            if (node is object)
            {
                throw new InvalidOperationException(node.GetType().FullName);
            }

            target = null;
            arguments = null;
            return false;
        }
    }
}
