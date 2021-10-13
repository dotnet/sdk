// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;

namespace Microsoft.CodeAnalysis.CSharp.NetAnalyzers.Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    public partial class CSharpAvoidMultipleEnumerationsAnalyzer
    {
        private sealed class CSharpInvocationCountDataFlowOperationVisitor : InvocationCountDataFlowOperationVisitor
        {
            public CSharpInvocationCountDataFlowOperationVisitor(
                GlobalFlowStateAnalysisContext analysisContext,
                ImmutableArray<IMethodSymbol> wellKnownDelayExecutionMethods,
                ImmutableArray<IMethodSymbol> wellKnownEnumerationMethods,
                IMethodSymbol? getEnumeratorMethod) : base(analysisContext, wellKnownDelayExecutionMethods, wellKnownEnumerationMethods, getEnumeratorMethod)
            {
            }

            protected override bool IsExpressionOfForEachStatement(SyntaxNode node)
                => node.Parent is ForEachStatementSyntax forEachStatementSyntax && forEachStatementSyntax.Expression.Equals(node);
        }
    }
}