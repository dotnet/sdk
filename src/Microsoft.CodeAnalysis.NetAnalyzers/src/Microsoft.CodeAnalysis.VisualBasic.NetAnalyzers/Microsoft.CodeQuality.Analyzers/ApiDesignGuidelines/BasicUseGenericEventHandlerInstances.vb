' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public Class BasicUseGenericEventHandlerInstancesAnalyzer
        Inherits UseGenericEventHandlerInstancesAnalyzer

        Protected Overrides Function IsAssignableTo(fromSymbol As ITypeSymbol, toSymbol As ITypeSymbol, compilation As Compilation) As Boolean
            Return fromSymbol IsNot Nothing AndAlso toSymbol IsNot Nothing AndAlso DirectCast(compilation, VisualBasicCompilation).ClassifyConversion(fromSymbol, toSymbol).IsWidening
        End Function
    End Class
End Namespace
