' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines

    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Partial Friend NotInheritable Class BasicAvoidMultipleEnumerationsAnalyzer
        Inherits AvoidMultipleEnumerations

        Friend Overrides Function CreateOperationVisitor(context As GlobalFlowStateDictionaryAnalysisContext, wellKnownSymbolsInfo As WellKnownSymbolsInfo) As GlobalFlowStateDictionaryFlowOperationVisitor
            Return New BasicInvocationCountDataFlowOperationVisitor(context, wellKnownSymbolsInfo)
        End Function

        Friend Overrides ReadOnly Property AvoidMultipleEnumerationsHelpers As AvoidMultipleEnumerationsHelpers
            Get
                Return BasicAvoidMultipleEnumerationsHelpers.Instance
            End Get
        End Property
    End Class
End Namespace