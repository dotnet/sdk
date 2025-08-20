' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.NetCore.Analyzers.InteropServices
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics

Namespace Microsoft.NetCore.VisualBasic.Analyzers.InteropServices
    ''' <summary>
    ''' CA2205: Use managed equivalents of win32 api
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicUseManagedEquivalentsOfWin32ApiAnalyzer
        Inherits UseManagedEquivalentsOfWin32ApiAnalyzer

    End Class
End Namespace