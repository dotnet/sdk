' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.NetCore.Analyzers.InteropServices
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics

Namespace Microsoft.NetCore.VisualBasic.Analyzers.InteropServices
    ''' <summary>
    ''' CA1414: Mark boolean PInvoke arguments with MarshalAs
    ''' </summary>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicMarkBooleanPInvokeArgumentsWithMarshalAsAnalyzer
        Inherits MarkBooleanPInvokeArgumentsWithMarshalAsAnalyzer

    End Class
End Namespace