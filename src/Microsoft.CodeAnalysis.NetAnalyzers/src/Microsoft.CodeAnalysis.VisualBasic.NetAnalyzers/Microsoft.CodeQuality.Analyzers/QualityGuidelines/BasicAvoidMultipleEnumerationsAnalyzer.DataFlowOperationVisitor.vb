' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.
Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations

Partial Public NotInheritable Class BasicAvoidMultipleEnumerationsAnalyzer

    Private Class BasicAvoidMultipleEnumerationsAnalyzer_DataFlowOperationVisitor
        Inherits AvoidMultipleEnumerations.InvocationCountDataFlowOperationVisitor

        Public Sub New(analysisContext As GlobalFlowStateAnalysisContext,
                       wellKnownDelayExecutionMethods As ImmutableArray(Of IMethodSymbol),
                       wellKnownEnumerationMethods As ImmutableArray(Of IMethodSymbol),
                       getEnumeratorMethod As IMethodSymbol)
            MyBase.New(analysisContext, wellKnownDelayExecutionMethods, wellKnownEnumerationMethods, getEnumeratorMethod)
        End Sub

        Protected Overrides Function IsExpressionOfForEachStatement(node As SyntaxNode) As Boolean
            Dim parent = TryCast(node.Parent, ForEachStatementSyntax)
            Return parent IsNot Nothing AndAlso parent.Expression.Equals(node)
        End Function
    End Class
End Class