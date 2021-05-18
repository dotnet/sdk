' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public Class BasicUseLiteralsWhereAppropriate
        Inherits UseLiteralsWhereAppropriateAnalyzer

        Protected Overrides Function IsConstantInterpolatedStringSupported(compilation As ParseOptions) As Boolean
            Return False
        End Function
    End Class
End Namespace
