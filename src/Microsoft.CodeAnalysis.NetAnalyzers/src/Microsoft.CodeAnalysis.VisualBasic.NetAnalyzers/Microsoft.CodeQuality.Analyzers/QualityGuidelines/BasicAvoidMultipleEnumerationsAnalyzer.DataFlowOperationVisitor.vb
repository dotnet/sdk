' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.
Imports System.Collections.Immutable
Imports Analyzer.Utilities.FlowAnalysis.Analysis.GlobalFlowStateDictionaryAnalysis
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines
    Partial Public NotInheritable Class BasicAvoidMultipleEnumerationsAnalyzer
        Private Class BasicInvocationCountDataFlowOperationVisitor
            Inherits AvoidMultipleEnumerationsFlowStateDictionaryFlowOperationVisitor

            Public Sub New(analysisContext As GlobalFlowStateDictionaryAnalysisContext,
                           oneParameterDeferredMethods As ImmutableArray(Of IMethodSymbol),
                           twoParametersDeferredMethods As ImmutableArray(Of IMethodSymbol),
                           oneParameterEnumeratedMethods As ImmutableArray(Of IMethodSymbol),
                           twoParametersEnumeratedMethods As ImmutableArray(Of IMethodSymbol),
                           additionalDeferredTypes As ImmutableArray(Of ITypeSymbol),
                           getEnumeratorMethod As IMethodSymbol)
                MyBase.New(analysisContext, oneParameterDeferredMethods, twoParametersDeferredMethods, oneParameterEnumeratedMethods, twoParametersEnumeratedMethods, additionalDeferredTypes, getEnumeratorMethod)
            End Sub

            Protected Overrides Function IsExpressionOfForEachStatement(node As SyntaxNode) As Boolean
                Dim parent = TryCast(node.Parent, ForEachStatementSyntax)
                Return parent IsNot Nothing AndAlso parent.Expression.Equals(node)
            End Function
        End Class
    End Class
End Namespace