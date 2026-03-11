// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations;

namespace Microsoft.CodeAnalysis.CSharp.NetAnalyzers.Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal sealed partial class CSharpAvoidMultipleEnumerationsAnalyzer : AvoidMultipleEnumerations
    {
        protected override bool IsExpressionOfForEachStatement(SyntaxNode syntax)
            => syntax.Parent is ForEachStatementSyntax forEachStatementSyntax && forEachStatementSyntax.Expression.Equals(syntax);
    }
}