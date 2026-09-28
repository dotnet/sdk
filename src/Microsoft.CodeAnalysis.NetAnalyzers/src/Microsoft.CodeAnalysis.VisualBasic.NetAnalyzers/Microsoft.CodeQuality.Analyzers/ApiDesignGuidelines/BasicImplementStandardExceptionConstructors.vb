' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines
    ''' <summary>
    ''' CA1032: Implement standard exception constructors
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicImplementStandardExceptionConstructorsAnalyzer
        Inherits ImplementStandardExceptionConstructorsAnalyzer

        Protected Overrides Function GetConstructorSignatureStringAndExceptionTypeParameter(symbol As ISymbol) As String
            Return "Public Sub New(message As String, innerException As Exception)"
        End Function

        Protected Overrides Function GetConstructorSignatureStringTypeParameter(symbol As ISymbol) As String
            Return "Public Sub New(message As String)"
        End Function

        Protected Overrides Function GetConstructorSignatureNoParameter(symbol As ISymbol) As String
            Return "Public Sub New()"
        End Function
    End Class
End Namespace