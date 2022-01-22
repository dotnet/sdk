' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.NetFramework.Analyzers
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics

Namespace Microsoft.NetFramework.VisualBasic.Analyzers
    ''' <summary>
    ''' CA1306: Set locale for data types
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicSetLocaleForDataTypesAnalyzer
        Inherits SetLocaleForDataTypesAnalyzer

    End Class
End Namespace