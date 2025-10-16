' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeQuality.Analyzers.Maintainability

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicMakeTypesInternal
        Inherits MakeTypesInternal

        Protected Overrides Function GetIdentifier(type As SyntaxNode) As SyntaxToken?
            Dim typeStatement = TryCast(type, TypeStatementSyntax)
            If typeStatement IsNot Nothing Then
                Return typeStatement.Identifier
            End If

            Dim enumStatement = TryCast(type, EnumStatementSyntax)
            If enumStatement IsNot Nothing Then
                Return enumStatement.Identifier
            End If

            Dim delegateStatement = TryCast(type, DelegateStatementSyntax)
            If delegateStatement IsNot Nothing Then
                Return delegateStatement.Identifier
            End If

            Return Nothing
        End Function
    End Class
End Namespace
