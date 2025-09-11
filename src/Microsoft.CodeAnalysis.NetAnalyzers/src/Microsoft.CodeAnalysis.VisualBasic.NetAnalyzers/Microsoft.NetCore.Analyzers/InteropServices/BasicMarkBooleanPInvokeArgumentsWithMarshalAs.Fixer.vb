' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.NetCore.Analyzers.InteropServices
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes

Namespace Microsoft.NetCore.VisualBasic.Analyzers.InteropServices
    ''' <summary>
    ''' CA1414: Mark boolean PInvoke arguments with MarshalAs
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicMarkBooleanPInvokeArgumentsWithMarshalAsFixer
        Inherits MarkBooleanPInvokeArgumentsWithMarshalAsFixer

    End Class
End Namespace
