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
                                                         oneParameterDeferredMethods As ImmutableArray(Of IMethodSymbol),
                                                         twoParametersDeferredMethods As ImmutableArray(Of IMethodSymbol),
                                                         oneParameterEnumeratedMethods As ImmutableArray(Of IMethodSymbol),
                                                         twoParametersEnumeratedMethods As ImmutableArray(Of IMethodSymbol),
                                                         additionalDeferredTypes As ImmutableArray(Of ITypeSymbol),
                                                         getEnumeratorMethod As IMethodSymbol) As GlobalFlowStateDictionaryFlowOperationVisitor
            Return New BasicInvocationCountDataFlowOperationVisitor(context, oneParameterDeferredMethods, twoParametersDeferredMethods, oneParameterEnumeratedMethods, twoParametersDeferredMethods, additionalDeferredTypes, getEnumeratorMethod)
        End Function
    End Class
End Namespace