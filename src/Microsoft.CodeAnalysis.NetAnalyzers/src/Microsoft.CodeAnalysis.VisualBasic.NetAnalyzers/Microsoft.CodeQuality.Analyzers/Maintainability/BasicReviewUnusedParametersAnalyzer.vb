' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeQuality.Analyzers.Maintainability

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability

    ''' <summary>
    ''' CA1801: Review unused parameters
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicReviewUnusedParametersAnalyzer
        Inherits ReviewUnusedParametersAnalyzer

        Protected Overrides Function IsPositionalRecordPrimaryConstructor(methodSymbol As IMethodSymbol) As Boolean
            Return False
        End Function
    End Class
End Namespace