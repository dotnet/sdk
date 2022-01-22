' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.NetFramework.Analyzers
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics

Namespace Microsoft.NetFramework.VisualBasic.Analyzers
    ''' <summary>
    ''' CA2212: Do not mark serviced components with WebMethod
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicDoNotMarkServicedComponentsWithWebMethodAnalyzer
        Inherits DoNotMarkServicedComponentsWithWebMethodAnalyzer

    End Class
End Namespace