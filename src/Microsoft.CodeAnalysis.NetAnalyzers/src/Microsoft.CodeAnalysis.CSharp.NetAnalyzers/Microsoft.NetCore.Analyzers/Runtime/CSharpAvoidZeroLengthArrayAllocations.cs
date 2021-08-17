// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.NetCore.Analyzers.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    /// <summary>
    /// RS0007: Avoid zero-length array allocations.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpAvoidZeroLengthArrayAllocationsAnalyzer : AvoidZeroLengthArrayAllocationsAnalyzer
    {
        protected override bool IsAttributeSyntax(SyntaxNode node)
        {
            return node is AttributeSyntax;
        }
    }
}