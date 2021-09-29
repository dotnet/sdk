' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.NetFramework.Analyzers
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes

Namespace Microsoft.NetFramework.VisualBasic.Analyzers
    ''' <summary>
    ''' CA1306: Set locale for data types
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicSetLocaleForDataTypesFixer
        Inherits SetLocaleForDataTypesFixer

    End Class
End Namespace
