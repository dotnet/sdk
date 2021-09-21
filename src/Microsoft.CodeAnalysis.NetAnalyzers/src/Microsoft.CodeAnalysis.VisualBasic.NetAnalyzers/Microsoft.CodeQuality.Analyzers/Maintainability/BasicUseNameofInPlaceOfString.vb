' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeQuality.Analyzers.Maintainability

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability

    ''' <summary>
    ''' CA1507: Use nameof to express symbol names
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicUseNameofInPlaceOfStringAnalyzer
        Inherits UseNameofInPlaceOfStringAnalyzer

        Protected Overrides Function IsApplicableToLanguageVersion(options As ParseOptions) As Boolean
            Return CType(options, VisualBasicParseOptions).LanguageVersion >= LanguageVersion.VisualBasic14
        End Function
    End Class
End Namespace