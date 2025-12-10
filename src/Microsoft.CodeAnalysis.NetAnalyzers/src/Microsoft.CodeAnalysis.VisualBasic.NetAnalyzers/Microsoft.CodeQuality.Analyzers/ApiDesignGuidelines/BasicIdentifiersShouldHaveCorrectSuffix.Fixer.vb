' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines
    ''' <summary>
    ''' CA1710: Identifiers should have correct suffix
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicIdentifiersShouldHaveCorrectSuffixFixer
        Inherits IdentifiersShouldHaveCorrectSuffixFixer

    End Class
End Namespace
