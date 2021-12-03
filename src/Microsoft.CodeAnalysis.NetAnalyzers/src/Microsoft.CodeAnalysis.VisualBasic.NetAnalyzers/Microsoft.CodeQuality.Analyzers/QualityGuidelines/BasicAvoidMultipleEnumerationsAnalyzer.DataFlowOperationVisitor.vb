' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines
    Partial Friend NotInheritable Class BasicAvoidMultipleEnumerationsAnalyzer
        Private Class BasicInvocationCountDataFlowOperationVisitor
            Inherits AvoidMultipleEnumerationsFlowStateDictionaryFlowOperationVisitor

            Public Sub New(analysisContext As GlobalFlowStateDictionaryAnalysisContext,
                           wellKnownSymbolsInfo As WellKnownSymbolsInfo)
                MyBase.New(analysisContext, extensionMethodCanBeReduced:=True, wellKnownSymbolsInfo)
            End Sub

            Protected Overrides Function IsExpressionOfForEachStatement(node As SyntaxNode) As Boolean
                Dim parent = TryCast(node.Parent, ForEachStatementSyntax)
                Return parent IsNot Nothing AndAlso parent.Expression.Equals(node)
            End Function
        End Class
    End Class
End Namespace