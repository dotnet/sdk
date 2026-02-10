' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeQuality.Analyzers.Documentation

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.Documentation
    ''' <summary>
    ''' CA1200: Avoid using cref tags with a prefix
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicAvoidUsingCrefTagsWithAPrefixFixer
        Inherits AvoidUsingCrefTagsWithAPrefixFixer

    End Class
End Namespace
