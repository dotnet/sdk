' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines

    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Partial Public NotInheritable Class BasicAvoidMultipleEnumerationsAnalyzer
        Inherits AvoidMultipleEnumerations

        Friend Overrides Function CreateOperationVisitor(context As GlobalFlowStateDictionaryAnalysisContext,
                                                         wellKnownSymbolsInfo As WellKnownSymbolsInfo,
                                                         getEnumeratorMethod As IMethodSymbol) As GlobalFlowStateDictionaryFlowOperationVisitor
            Return New BasicInvocationCountDataFlowOperationVisitor(context, wellKnownSymbolsInfo, getEnumeratorMethod)
        End Function
    End Class
End Namespace