' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    ''' <summary>
    ''' CA2239: Provide deserialization methods for optional fields
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicProvideDeserializationMethodsForOptionalFieldsFixer
        Inherits ProvideDeserializationMethodsForOptionalFieldsFixer

    End Class
End Namespace
