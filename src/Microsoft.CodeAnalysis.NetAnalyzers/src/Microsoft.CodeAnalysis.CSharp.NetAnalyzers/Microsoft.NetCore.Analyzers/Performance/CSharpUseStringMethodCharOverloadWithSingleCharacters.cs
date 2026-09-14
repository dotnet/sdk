// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
