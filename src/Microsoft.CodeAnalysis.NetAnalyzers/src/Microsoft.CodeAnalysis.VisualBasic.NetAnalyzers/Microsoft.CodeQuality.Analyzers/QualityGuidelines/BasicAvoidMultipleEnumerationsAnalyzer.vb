' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.
Imports System.Collections.Immutable
Imports Analyzer.Utilities.FlowAnalysis.Analysis.GlobalFlowStateDictionaryAnalysis
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines

    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Partial Public NotInheritable Class BasicAvoidMultipleEnumerationsAnalyzer
        Inherits AvoidMultipleEnumerations

        Friend Overrides Function CreateOperationVisitor(context As GlobalFlowStateDictionaryAnalysisContext,
                                                         wellKnownDeferredExecutionMethodsTakeOneIEnumerable As ImmutableArray(Of IMethodSymbol),
                                                         wellKnownDeferredExecutionMethodsTakeTwoIEnumerables As ImmutableArray(Of IMethodSymbol),
                                                         wellKnownEnumerationMethods As ImmutableArray(Of IMethodSymbol),
                                                         getEnumeratorMethod As IMethodSymbol) As GlobalFlowStateDictionaryFlowOperationVisitor
            Return New BasicInvocationCountDataFlowOperationVisitor(context, wellKnownDeferredExecutionMethodsTakeOneIEnumerable, wellKnownDeferredExecutionMethodsTakeTwoIEnumerables, wellKnownEnumerationMethods, getEnumeratorMethod)
        End Function
    End Class
End Namespace