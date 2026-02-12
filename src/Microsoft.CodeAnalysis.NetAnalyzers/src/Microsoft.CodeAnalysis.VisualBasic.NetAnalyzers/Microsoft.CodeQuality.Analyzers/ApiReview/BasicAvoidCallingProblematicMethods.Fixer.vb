' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeQuality.Analyzers.ApiReview
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.ApiReview
    ''' <summary>
    ''' CA2001: Avoid calling problematic methods
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicAvoidCallingProblematicMethodsFixer
        Inherits AvoidCallingProblematicMethodsFixer

    End Class
End Namespace
