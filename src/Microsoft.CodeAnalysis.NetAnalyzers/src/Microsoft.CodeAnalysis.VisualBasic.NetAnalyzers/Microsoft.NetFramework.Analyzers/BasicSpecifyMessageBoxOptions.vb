' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.NetFramework.Analyzers
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics

Namespace Microsoft.NetFramework.VisualBasic.Analyzers
    ''' <summary>
    ''' CA1300: Specify MessageBoxOptions
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicSpecifyMessageBoxOptionsAnalyzer
        Inherits SpecifyMessageBoxOptionsAnalyzer

    End Class
End Namespace