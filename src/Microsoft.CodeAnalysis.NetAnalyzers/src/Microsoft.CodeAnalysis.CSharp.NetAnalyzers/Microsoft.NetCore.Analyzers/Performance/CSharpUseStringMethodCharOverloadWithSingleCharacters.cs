// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.CodeAnalysis.CSharp.NetAnalyzers.Microsoft.NetCore.Analyzers.Performance
{
    /// <inheritdoc/>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpUseStringMethodCharOverloadWithSingleCharacters : UseStringMethodCharOverloadWithSingleCharacters
    {
        protected override SyntaxNode? GetArgumentList(SyntaxNode argumentNode)
        {
            return argumentNode.FirstAncestorOrSelf<ArgumentListSyntax>();
        }
    }
}
