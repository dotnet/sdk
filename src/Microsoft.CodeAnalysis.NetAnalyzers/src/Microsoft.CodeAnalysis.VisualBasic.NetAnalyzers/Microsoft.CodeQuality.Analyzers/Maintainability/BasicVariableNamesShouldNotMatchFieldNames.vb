' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeQuality.Analyzers.Maintainability

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability
    ''' <summary>
    ''' CA1500: Variable names should not match field names
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicVariableNamesShouldNotMatchFieldNamesAnalyzer
        Inherits VariableNamesShouldNotMatchFieldNamesAnalyzer

    End Class
End Namespace