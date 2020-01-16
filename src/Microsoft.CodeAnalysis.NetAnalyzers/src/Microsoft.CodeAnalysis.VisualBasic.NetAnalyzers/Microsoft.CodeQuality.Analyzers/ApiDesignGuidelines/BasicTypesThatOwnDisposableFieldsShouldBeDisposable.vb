' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Threading
Imports Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicTypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer
        Inherits TypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer(Of TypeBlockSyntax)
        Protected Overrides Function GetAnalyzer(compilation As Compilation) As DisposableFieldAnalyzer
            Return New BasicDisposableFieldAnalyzer(compilation)
        End Function

        Private Class BasicDisposableFieldAnalyzer
            Inherits DisposableFieldAnalyzer
            Public Sub New(compilation As Compilation)
                MyBase.New(compilation)
            End Sub

            Protected Overrides Function IsDisposableFieldCreation(node As SyntaxNode, model As SemanticModel, disposableFields As HashSet(Of ISymbol), cancellationToken As CancellationToken) As Boolean
                If TypeOf node Is AssignmentStatementSyntax Then
                    Dim assignment = DirectCast(node, AssignmentStatementSyntax)
                    If TypeOf assignment.Right Is ObjectCreationExpressionSyntax AndAlso disposableFields.Contains(model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol) Then
                        Return True
                    End If
                ElseIf TypeOf node Is FieldDeclarationSyntax Then
                    Dim fieldDecls = DirectCast(node, FieldDeclarationSyntax).Declarators
                    For Each declarator As VariableDeclaratorSyntax In fieldDecls
                        'Explicit initialization is not permitted with multiple variables declared with a single type specifier
                        If declarator.Names.Count > 1 Then
                            Continue For
                        End If
                        Dim fieldName = declarator.Names.First()
                        If TypeOf declarator?.Initializer?.Value Is ObjectCreationExpressionSyntax AndAlso disposableFields.Contains(model.GetDeclaredSymbol(fieldName, cancellationToken)) Then
                            Return True
                        End If
                    Next
                ElseIf TypeOf node Is NamedFieldInitializerSyntax Then
                    Dim fieldInit = DirectCast(node, NamedFieldInitializerSyntax)
                    If TypeOf fieldInit.Expression Is ObjectCreationExpressionSyntax AndAlso disposableFields.Contains(model.GetSymbolInfo(fieldInit.Name, cancellationToken).Symbol) Then
                        Return True
                    End If
                End If
                Return False
            End Function
        End Class
    End Class
End Namespace

