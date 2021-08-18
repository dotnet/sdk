' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.NetCore.Analyzers.Runtime
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    ''' <summary>
    ''' CA2207: Initialize value type static fields inline
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicInitializeStaticFieldsInlineFixer
        Inherits InitializeStaticFieldsInlineFixer(Of SyntaxKind)

    End Class
End Namespace
