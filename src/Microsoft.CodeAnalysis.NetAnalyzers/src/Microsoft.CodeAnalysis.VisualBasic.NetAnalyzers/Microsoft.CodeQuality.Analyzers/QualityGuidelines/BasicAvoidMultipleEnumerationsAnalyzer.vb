' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines

    <Diagnostics.DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Partial Public NotInheritable Class BasicAvoidMultipleEnumerationsAnalyzer
        Inherits AvoidMultipleEnumerations

        Friend Overrides Function CreateOperationVisitor(context As GlobalFlowStateAnalysisContext, wellKnownDelayExecutionMethods As ImmutableArray(Of IMethodSymbol), wellKnownEnumerationMethods As ImmutableArray(Of IMethodSymbol), getEnumeratorMethod As IMethodSymbol) As GlobalFlowStateValueSetFlowOperationVisitor
            Return New BasicInvocationCountDataFlowOperationVisitor(context, wellKnownDelayExecutionMethods, wellKnownEnumerationMethods, getEnumeratorMethod)
        End Function
    End Class
End Namespace