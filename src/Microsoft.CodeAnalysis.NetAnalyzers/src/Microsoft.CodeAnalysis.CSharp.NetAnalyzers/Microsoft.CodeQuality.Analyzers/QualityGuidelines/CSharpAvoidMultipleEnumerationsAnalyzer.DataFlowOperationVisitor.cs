// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis;

namespace Microsoft.CodeAnalysis.CSharp.NetAnalyzers.Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    public partial class CSharpAvoidMultipleEnumerationsAnalyzer
    {
        private sealed class CSharpInvocationCountValueSetFlowStateDictionaryFlowOperationVisitor : AvoidMultipleEnumerationsFlowStateDictionaryFlowOperationVisitor
        {
            public CSharpInvocationCountValueSetFlowStateDictionaryFlowOperationVisitor(
                GlobalFlowStateDictionaryAnalysisContext context,
                WellKnownSymbolsInfo wellKnownSymbolsInfo) : base(
                    context,
                    wellKnownSymbolsInfo)
            {
            }

            protected override bool IsExpressionOfForEachStatement(SyntaxNode node)
                => node.Parent is ForEachStatementSyntax forEachStatementSyntax && forEachStatementSyntax.Expression.Equals(node);
        }
    }
}