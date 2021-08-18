' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeQuality.Analyzers.ApiReview
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.ApiReview
    ''' <summary>
    ''' CA2001: Avoid calling problematic methods
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicAvoidCallingProblematicMethodsAnalyzer
        Inherits AvoidCallingProblematicMethodsAnalyzer

    End Class
End Namespace