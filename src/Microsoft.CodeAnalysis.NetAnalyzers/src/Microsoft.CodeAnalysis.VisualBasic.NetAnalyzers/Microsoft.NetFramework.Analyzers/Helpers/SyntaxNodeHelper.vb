' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.NetFramework.Analyzers.Helpers
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.NetFramework.VisualBasic.Analyzers.Helpers

    Public NotInheritable Class BasicSyntaxNodeHelper
        Inherits SyntaxNodeHelper

        Public Shared ReadOnly Property DefaultInstance As BasicSyntaxNodeHelper = New BasicSyntaxNodeHelper()

        Private Sub New()
        End Sub

        Public Overrides Function GetClassDeclarationTypeSymbol(node As SyntaxNode, semanticModel As SemanticModel) As ITypeSymbol
            If node Is Nothing Then
                Return Nothing
            End If

            Dim kind As SyntaxKind = node.Kind()
            If kind = SyntaxKind.ClassBlock Then
                Return semanticModel.GetDeclaredSymbol(CType(node, ClassBlockSyntax))
            End If

            Return Nothing
        End Function

        Public Overrides Function GetAssignmentLeftNode(node As SyntaxNode) As SyntaxNode
            If node Is Nothing Then
                Return Nothing
            End If

            Dim kind As SyntaxKind = node.Kind()
            If kind = SyntaxKind.SimpleAssignmentStatement Then
                Return CType(node, AssignmentStatementSyntax).Left
            End If

            If kind = SyntaxKind.VariableDeclarator Then
                Return CType(node, VariableDeclaratorSyntax).Names.First()
            End If

            If kind = SyntaxKind.NamedFieldInitializer Then
                Return CType(node, NamedFieldInitializerSyntax).Name
            End If

            Return Nothing
        End Function

        Public Overrides Function GetAssignmentRightNode(node As SyntaxNode) As SyntaxNode
            If node Is Nothing Then
                Return Nothing
            End If

            Dim kind As SyntaxKind = node.Kind()
            If kind = SyntaxKind.SimpleAssignmentStatement Then
                Return CType(node, AssignmentStatementSyntax).Right
            End If

            If kind = SyntaxKind.VariableDeclarator Then
                Dim decl As VariableDeclaratorSyntax = CType(node, VariableDeclaratorSyntax)

                If decl.Initializer IsNot Nothing Then
                    Return decl.Initializer.Value
                End If

                If decl.AsClause IsNot Nothing Then
                    Return decl.AsClause
                End If
            End If

            If kind = SyntaxKind.NamedFieldInitializer Then
                Return CType(node, NamedFieldInitializerSyntax).Expression
            End If

            Return Nothing
        End Function

        Public Overrides Function GetMemberAccessExpressionNode(node As SyntaxNode) As SyntaxNode
            If node Is Nothing Then
                Return Nothing
            End If

            Dim kind As SyntaxKind = node.Kind()
            If kind = SyntaxKind.SimpleMemberAccessExpression Then
                Return CType(node, MemberAccessExpressionSyntax).Expression
            End If

            Return Nothing
        End Function

        Public Overrides Function GetMemberAccessNameNode(node As SyntaxNode) As SyntaxNode
            If node Is Nothing Then
                Return Nothing
            End If

            Dim kind As SyntaxKind = node.Kind()
            If kind = SyntaxKind.SimpleMemberAccessExpression Then
                Return CType(node, MemberAccessExpressionSyntax).Name
            End If

            Return Nothing
        End Function

        Public Overrides Function GetInvocationExpressionNode(node As SyntaxNode) As SyntaxNode
            If node Is Nothing Then
                Return Nothing
            End If

            Dim kind As SyntaxKind = node.Kind()
            If kind = SyntaxKind.InvocationExpression Then
                Return CType(node, InvocationExpressionSyntax).Expression
            End If

            Return Nothing
        End Function

        Public Overrides Function GetCallTargetNode(node As SyntaxNode) As SyntaxNode
            If node Is Nothing Then
                Return Nothing
            End If

            Dim kind As SyntaxKind = node.Kind()
            If kind = SyntaxKind.InvocationExpression Then
                Dim callExpr As ExpressionSyntax = CType(node, InvocationExpressionSyntax).Expression
                Dim nameNode As SyntaxNode = GetMemberAccessNameNode(callExpr)
                If nameNode IsNot Nothing Then
                    Return nameNode
                Else
                    Return callExpr
                End If
            ElseIf kind = SyntaxKind.ObjectCreationExpression Then
                Return CType(node, ObjectCreationExpressionSyntax).Type
            End If

            Return Nothing
        End Function

        Public Overrides Function GetDefaultValueForAnOptionalParameter(declNode As SyntaxNode, paramIndex As Integer) As SyntaxNode
            Dim methodDecl = TryCast(declNode, MethodBlockBaseSyntax)
            If methodDecl Is Nothing Then
                Return Nothing
            End If

            Dim paramList As ParameterListSyntax = methodDecl.BlockStatement.ParameterList
            If paramIndex < paramList.Parameters.Count Then
                Dim equalsValueNode As EqualsValueSyntax = paramList.Parameters(paramIndex).Default
                If equalsValueNode IsNot Nothing Then
                    Return equalsValueNode.Value
                End If
            End If

            Return Nothing
        End Function

        Protected Overrides Function GetCallArgumentExpressionNodes(node As SyntaxNode, callKind As CallKinds) As IEnumerable(Of SyntaxNode)
            If node Is Nothing Then
                Return Nothing
            End If

            Dim argList As ArgumentListSyntax = Nothing
            Dim kind As SyntaxKind = node.Kind()

            If kind = SyntaxKind.InvocationExpression AndAlso ((callKind And CallKinds.Invocation) <> 0) Then
                argList = CType(node, InvocationExpressionSyntax).ArgumentList
            ElseIf (kind = SyntaxKind.ObjectCreationExpression) AndAlso ((callKind And CallKinds.ObjectCreation) <> 0) Then
                argList = CType(node, ObjectCreationExpressionSyntax).ArgumentList
            ElseIf (kind = SyntaxKind.AsNewClause) AndAlso ((callKind And CallKinds.ObjectCreation) <> 0) Then
                Dim asNewClause As AsNewClauseSyntax = CType(node, AsNewClauseSyntax)
                If asNewClause.NewExpression IsNot Nothing AndAlso asNewClause.NewExpression.Kind = SyntaxKind.ObjectCreationExpression Then
                    argList = CType(asNewClause.NewExpression, ObjectCreationExpressionSyntax).ArgumentList
                End If
            End If

            If argList IsNot Nothing Then
                Return From arg In argList.Arguments
                       Select arg.GetExpression()
            End If

            Return Enumerable.Empty(Of SyntaxNode)
        End Function

        Public Overrides Function GetObjectInitializerExpressionNodes(node As SyntaxNode) As IEnumerable(Of SyntaxNode)
            Dim empty As IEnumerable(Of SyntaxNode) = Enumerable.Empty(Of SyntaxNode)

            If node Is Nothing Then
                Return empty
            End If

            Dim objectCreationNode As ObjectCreationExpressionSyntax = node.DescendantNodesAndSelf().OfType(Of ObjectCreationExpressionSyntax)().FirstOrDefault()

            If objectCreationNode Is Nothing Then
                Return empty
            End If

            If objectCreationNode.Initializer Is Nothing Then
                Return empty
            End If

            Dim kind As SyntaxKind = objectCreationNode.Initializer.Kind()
            If kind <> SyntaxKind.ObjectMemberInitializer Then
                Return empty
            End If

            ' CA1804: Remove unused locals
            Dim initializer As ObjectMemberInitializerSyntax = CType(objectCreationNode.Initializer, ObjectMemberInitializerSyntax)
            Return From fieldInitializer In initializer.Initializers
                   Where fieldInitializer.Kind() = SyntaxKind.NamedFieldInitializer
                   Select CType(fieldInitializer, NamedFieldInitializerSyntax)
        End Function

        Public Overrides Function IsMethodInvocationNode(node As SyntaxNode) As Boolean
            If node Is Nothing Then
                Return False
            End If

            Dim kind As SyntaxKind = node.Kind()
            Return kind = SyntaxKind.InvocationExpression OrElse kind = SyntaxKind.ObjectCreationExpression
        End Function

        Public Overrides Function GetCalleeMethodSymbol(node As SyntaxNode, semanticModel As SemanticModel) As IMethodSymbol
            If node Is Nothing Then
                Return Nothing
            End If

            Dim kind As SyntaxKind = node.Kind()
            Dim symbol As ISymbol = GetReferencedSymbol(node, semanticModel)

            If symbol Is Nothing And kind = SyntaxKind.AsNewClause Then
                symbol = GetReferencedSymbol(node.ChildNodes().First(), semanticModel)
            End If

            If symbol IsNot Nothing Then
                If symbol.Kind = SymbolKind.Method Then
                    Return CType(symbol, IMethodSymbol)
                End If
            End If

            Return Nothing
        End Function
        Public Overrides Function GetCallerMethodSymbol(node As SyntaxNode, semanticModel As SemanticModel) As IMethodSymbol
            If node Is Nothing Then
                Return Nothing
            End If

            Dim declaration As MethodBlockSyntax = node.AncestorsAndSelf().OfType(Of MethodBlockSyntax)().FirstOrDefault()

            If declaration IsNot Nothing Then
                Return semanticModel.GetDeclaredSymbol(declaration)
            End If

            Dim constructor As SubNewStatementSyntax = node.AncestorsAndSelf().OfType(Of SubNewStatementSyntax)().FirstOrDefault()

            If constructor IsNot Nothing Then
                Return semanticModel.GetDeclaredSymbol(constructor)
            End If

            Return Nothing
        End Function

        Public Overrides Function GetEnclosingTypeSymbol(node As SyntaxNode, semanticModel As SemanticModel) As ITypeSymbol
            If node Is Nothing Then
                Return Nothing
            End If

            Dim declaration As ClassBlockSyntax = node.AncestorsAndSelf().OfType(Of ClassBlockSyntax)().FirstOrDefault()

            If declaration Is Nothing Then
                Return Nothing
            End If

            Return semanticModel.GetDeclaredSymbol(declaration)
        End Function

        Public Overrides Function GetDescendantAssignmentExpressionNodes(node As SyntaxNode) As IEnumerable(Of SyntaxNode)
            Dim empty As IEnumerable(Of SyntaxNode) = Enumerable.Empty(Of SyntaxNode)()

            If node Is Nothing Then
                Return empty
            End If

            Return node.DescendantNodesAndSelf.OfType(Of AssignmentStatementSyntax)()
        End Function

        Public Overrides Function GetDescendantMemberAccessExpressionNodes(node As SyntaxNode) As IEnumerable(Of SyntaxNode)
            Dim empty As IEnumerable(Of SyntaxNode) = Enumerable.Empty(Of SyntaxNode)()

            If node Is Nothing Then
                Return empty
            End If

            Return node.DescendantNodesAndSelf().OfType(Of MemberAccessExpressionSyntax)()
        End Function

        Public Overrides Function IsObjectCreationExpressionUnderFieldDeclaration(node As SyntaxNode) As Boolean
            Return node IsNot Nothing And node.Kind() = SyntaxKind.ObjectCreationExpression _
                And node.AncestorsAndSelf().OfType(Of FieldDeclarationSyntax)().FirstOrDefault() IsNot Nothing
        End Function

        Public Overrides Function GetVariableDeclaratorOfAFieldDeclarationNode(objectCreationExpression As SyntaxNode) As SyntaxNode
            If IsObjectCreationExpressionUnderFieldDeclaration(objectCreationExpression) Then
                Return objectCreationExpression.AncestorsAndSelf().OfType(Of VariableDeclaratorSyntax)().FirstOrDefault()
            Else
                Return Nothing
            End If
        End Function
    End Class
End Namespace
