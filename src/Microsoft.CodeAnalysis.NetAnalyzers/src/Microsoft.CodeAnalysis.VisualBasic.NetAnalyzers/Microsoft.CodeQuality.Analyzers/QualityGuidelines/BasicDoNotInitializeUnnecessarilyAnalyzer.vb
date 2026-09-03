' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines
    <Diagnostics.DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public Class BasicDoNotInitializeUnnecessarilyAnalyzer
        Inherits DoNotInitializeUnnecessarilyAnalyzer

        Protected Overrides Function IsNullSuppressed(op As IOperation) As Boolean
            Return False
        End Function
    End Class
End Namespace

