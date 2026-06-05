' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Performance

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Performance
    ''' <inheritdoc/>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicUseStringMethodCharOverloadWithSingleCharacters
        Inherits UseStringMethodCharOverloadWithSingleCharacters

        Protected Overrides Function GetArgumentList(argumentNode As SyntaxNode) As SyntaxNode
            Return argumentNode.FirstAncestorOrSelf(Of ArgumentListSyntax)()
        End Function

    End Class
End Namespace
