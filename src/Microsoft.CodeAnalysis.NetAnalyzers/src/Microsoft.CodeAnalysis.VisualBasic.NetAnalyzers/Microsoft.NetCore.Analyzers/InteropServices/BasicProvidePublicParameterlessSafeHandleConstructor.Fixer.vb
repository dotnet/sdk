' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.NetCore.Analyzers.InteropServices
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes

Namespace Microsoft.NetCore.VisualBasic.Analyzers.InteropServices
    ''' <summary>
    ''' CA1418: Provide a public parameterless constructor for concrete types derived from System.Runtime.InteropServices.SafeHandle
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicProvidePublicParameterlessSafeHandleConstructorFixer
        Inherits ProvidePublicParameterlessSafeHandleConstructorFixer

    End Class
End Namespace
