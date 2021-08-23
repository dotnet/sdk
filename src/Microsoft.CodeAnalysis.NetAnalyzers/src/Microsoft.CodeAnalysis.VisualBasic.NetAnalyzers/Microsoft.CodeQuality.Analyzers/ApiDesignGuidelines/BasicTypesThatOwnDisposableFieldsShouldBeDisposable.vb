' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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

            Protected Overrides Iterator Function GetDisposableFieldCreations(node As SyntaxNode, model As SemanticModel,
                                                                              disposableFields As HashSet(Of ISymbol),
                                                                              cancellationToken As CancellationToken) As IEnumerable(Of IFieldSymbol)
                If TypeOf node Is AssignmentStatementSyntax Then
                    Dim assignment = DirectCast(node, AssignmentStatementSyntax)
                    If TypeOf assignment.Right Is ObjectCreationExpressionSyntax Then
                        Dim field = TryCast(model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol, IFieldSymbol)
                        If disposableFields.Contains(field) Then
                            Yield field
                        End If
                    End If
                ElseIf TypeOf node Is FieldDeclarationSyntax Then
                    Dim fieldDecls = DirectCast(node, FieldDeclarationSyntax).Declarators
                    For Each declarator As VariableDeclaratorSyntax In fieldDecls
                        'Explicit initialization is not permitted with multiple variables declared with a single type specifier
                        If declarator.Names.Count > 1 Then
                            Continue For
                        End If
                        Dim firstFieldName = declarator.Names.First()
                        If TypeOf declarator?.Initializer?.Value Is ObjectCreationExpressionSyntax Then
                            Dim field = TryCast(model.GetDeclaredSymbol(firstFieldName, cancellationToken), IFieldSymbol)
                            If disposableFields.Contains(field) Then
                                Yield field
                            End If
                        End If
                    Next
                ElseIf TypeOf node Is NamedFieldInitializerSyntax Then
                    Dim fieldInit = DirectCast(node, NamedFieldInitializerSyntax)
                    If TypeOf fieldInit.Expression Is ObjectCreationExpressionSyntax Then
                        Dim field = TryCast(model.GetSymbolInfo(fieldInit.Name, cancellationToken).Symbol, IFieldSymbol)
                        If disposableFields.Contains(field) Then
                            Yield field
                        End If
                    End If
                End If
            End Function
        End Class
    End Class
End Namespace

