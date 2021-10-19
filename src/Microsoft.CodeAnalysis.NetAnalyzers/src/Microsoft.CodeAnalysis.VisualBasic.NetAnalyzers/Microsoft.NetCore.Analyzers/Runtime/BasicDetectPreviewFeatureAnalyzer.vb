' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.NetCore.Analyzers.Runtime
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicDetectPreviewFeatureAnalyzer

        Inherits DetectPreviewFeatureAnalyzer

        Private Function IsSyntaxToken(identifier As SyntaxToken, previewInterfaceSymbol As ISymbol) As Boolean
            Return identifier.ValueText.Equals(previewInterfaceSymbol.Name, StringComparison.OrdinalIgnoreCase)
        End Function

        Private Function GetElementTypeForNullableAndArrayTypeNodes(parameterType As TypeSyntax) As TypeSyntax
            Dim ret As TypeSyntax = parameterType
            Dim loopVariable = TryCast(parameterType, NullableTypeSyntax)
            While loopVariable IsNot Nothing
                ret = loopVariable.ElementType
                loopVariable = TryCast(ret, NullableTypeSyntax)
            End While

            Dim arrayLoopVariable = TryCast(ret, ArrayTypeSyntax)
            While arrayLoopVariable IsNot Nothing
                ret = arrayLoopVariable.ElementType
                arrayLoopVariable = TryCast(ret, ArrayTypeSyntax)
            End While

            Return ret
        End Function

        Private Function IsIdentifierNameSyntax(identifier As TypeSyntax, previewInterfaceSymbol As ISymbol) As Boolean
            Dim identifierName = TryCast(identifier, IdentifierNameSyntax)
            If identifierName IsNot Nothing AndAlso IsSyntaxToken(identifierName.Identifier, previewInterfaceSymbol) Then
                Return True
            End If

            Dim nullable = TryCast(identifier, NullableTypeSyntax)
            If nullable IsNot Nothing AndAlso IsIdentifierNameSyntax(nullable.ElementType, previewInterfaceSymbol) Then
                Return True
            End If

            Return False
        End Function

        Private Function TryMatchGenericSyntaxNodeWithGivenSymbol(genericName As GenericNameSyntax, previewReturnTypeSymbol As ISymbol, ByRef syntaxNode As SyntaxNode) As Boolean
            If IsSyntaxToken(genericName.Identifier, previewReturnTypeSymbol) Then
                syntaxNode = genericName
                Return True
            End If

            Dim typeArgumentList = genericName.TypeArgumentList
            For Each typeArgument In typeArgumentList.Arguments
                Dim typeArgumentElementType = GetElementTypeForNullableAndArrayTypeNodes(typeArgument)
                Dim innerGenericName = TryCast(typeArgumentElementType, GenericNameSyntax)
                If innerGenericName IsNot Nothing Then
                    If TryMatchGenericSyntaxNodeWithGivenSymbol(innerGenericName, previewReturnTypeSymbol, syntaxNode) Then
                        Return True
                    End If
                End If

                If IsIdentifierNameSyntax(typeArgumentElementType, previewReturnTypeSymbol) Then
                    syntaxNode = typeArgumentElementType
                    Return True
                End If
            Next

            syntaxNode = Nothing
            Return False
        End Function

        Private Function TryGetPreviewInterfaceNodeForClassOrStructImplementingPreviewInterface(baseListTypes As SyntaxList(Of InheritsStatementSyntax), previewInterfaceSymbol As ISymbol, ByRef previewInterfaceNode As SyntaxNode) As Boolean
            For Each baseTypeSyntax In baseListTypes
                Dim baseTypes = baseTypeSyntax.Types
                For Each baseType In baseTypes
                    If TryGetPreviewInterfaceNodeForClassOrStructImplementingPreviewInterface(baseType, previewInterfaceSymbol, previewInterfaceNode) Then
                        Return True
                    End If
                Next
            Next

            previewInterfaceNode = Nothing
            Return False
        End Function

        Private Function TryGetPreviewInterfaceNodeForClassOrStructImplementingPreviewInterface(baseType As TypeSyntax, previewInterfaceSymbol As ISymbol, ByRef previewInterfaceNode As SyntaxNode) As Boolean
            Dim identifier = TryCast(baseType, IdentifierNameSyntax)
            If identifier IsNot Nothing AndAlso IsSyntaxToken(identifier.Identifier, previewInterfaceSymbol) Then
                previewInterfaceNode = baseType
                Return True
            End If

            Dim generic = TryCast(baseType, GenericNameSyntax)
            If generic IsNot Nothing Then
                Dim previewConstraint As SyntaxNode = Nothing
                If TryMatchGenericSyntaxNodeWithGivenSymbol(generic, previewInterfaceSymbol, previewConstraint) Then
                    previewInterfaceNode = previewConstraint
                    Return True
                End If
            End If

            previewInterfaceNode = Nothing
            Return False
        End Function

        Private Function TryGetPreviewInterfaceNodeForClassOrStructImplementingPreviewInterface(baseListTypes As SyntaxList(Of ImplementsStatementSyntax), previewInterfaceSymbol As ISymbol, ByRef previewInterfaceNode As SyntaxNode) As Boolean
            For Each baseTypeSyntax In baseListTypes
                Dim baseTypes = baseTypeSyntax.Types
                For Each baseType In baseTypes
                    If TryGetPreviewInterfaceNodeForClassOrStructImplementingPreviewInterface(baseType, previewInterfaceSymbol, previewInterfaceNode) Then
                        Return True
                    End If
                Next
            Next

            previewInterfaceNode = Nothing
            Return False
        End Function

        Protected Overrides Function GetPreviewInterfaceNodeForTypeImplementingPreviewInterface(typeSymbol As ISymbol, previewInterfaceSymbol As ISymbol) As SyntaxNode
            Dim typeSymbolDeclaringReferences = typeSymbol.DeclaringSyntaxReferences

            For Each syntaxReference In typeSymbolDeclaringReferences
                Dim typeSymbolDefinition = syntaxReference.GetSyntax()
                Dim classStatement = TryCast(typeSymbolDefinition, ClassStatementSyntax)
                If classStatement IsNot Nothing Then
                    Dim classBlock = TryCast(classStatement.Parent, ClassBlockSyntax)
                    If classBlock IsNot Nothing Then
                        Dim syntaxNode As SyntaxNode = Nothing
                        If TryGetPreviewInterfaceNodeForClassOrStructImplementingPreviewInterface(classBlock.Inherits, previewInterfaceSymbol, syntaxNode) Then
                            Return syntaxNode
                        Else
                            If TryGetPreviewInterfaceNodeForClassOrStructImplementingPreviewInterface(classBlock.Implements, previewInterfaceSymbol, syntaxNode) Then
                                Return syntaxNode
                            End If
                        End If
                    End If
                End If

                Dim structStatement = TryCast(typeSymbolDefinition, StructureStatementSyntax)
                If structStatement IsNot Nothing Then
                    Dim structBlock = TryCast(structStatement.Parent, StructureBlockSyntax)
                    If structBlock IsNot Nothing Then
                        Dim syntaxNode As SyntaxNode = Nothing
                        If TryGetPreviewInterfaceNodeForClassOrStructImplementingPreviewInterface(structBlock.Inherits, previewInterfaceSymbol, syntaxNode) Then
                            Return syntaxNode
                        Else
                            If TryGetPreviewInterfaceNodeForClassOrStructImplementingPreviewInterface(structBlock.Implements, previewInterfaceSymbol, syntaxNode) Then
                                Return syntaxNode
                            End If
                        End If
                    End If
                End If

                Dim interfaceStatement = TryCast(typeSymbolDefinition, InterfaceStatementSyntax)
                If interfaceStatement IsNot Nothing Then
                    Dim interfaceBlock = TryCast(interfaceStatement.Parent, InterfaceBlockSyntax)
                    If interfaceBlock IsNot Nothing Then
                        Dim syntaxNode As SyntaxNode = Nothing
                        If TryGetPreviewInterfaceNodeForClassOrStructImplementingPreviewInterface(interfaceBlock.Implements, previewInterfaceSymbol, syntaxNode) Then
                            Return syntaxNode
                        Else
                            If TryGetPreviewInterfaceNodeForClassOrStructImplementingPreviewInterface(interfaceBlock.Inherits, previewInterfaceSymbol, syntaxNode) Then
                                Return syntaxNode
                            End If
                        End If
                    End If
                End If
            Next

            Return Nothing
        End Function

        Protected Overrides Function GetConstraintSyntaxNodeForTypeConstrainedByPreviewTypes(typeOrMethodSymbol As ISymbol, previewInterfaceConstraintSymbol As ISymbol) As SyntaxNode
            Return Nothing
        End Function

        Private Function TryGetNodeFromAsClauseForMethodOrProperty(asClause As AsClauseSyntax, previewReturnTypeSymbol As ISymbol) As SyntaxNode
            Dim simpleAsClause = TryCast(asClause, SimpleAsClauseSyntax)
            If simpleAsClause IsNot Nothing Then
                Dim returnType = simpleAsClause.Type
                returnType = GetElementTypeForNullableAndArrayTypeNodes(returnType)
                If IsIdentifierNameSyntax(returnType, previewReturnTypeSymbol) Then
                    Return returnType
                End If

                Dim genericName = TryCast(returnType, GenericNameSyntax)
                If genericName IsNot Nothing Then
                    Dim previewNode As SyntaxNode = Nothing
                    If TryMatchGenericSyntaxNodeWithGivenSymbol(genericName, previewReturnTypeSymbol, previewNode) Then
                        Return previewNode
                    End If
                End If
            End If

            Return Nothing
        End Function
        Protected Overrides Function GetPreviewReturnTypeSyntaxNodeForMethodOrProperty(methodOrPropertySymbol As ISymbol, previewReturnTypeSymbol As ISymbol) As SyntaxNode
            Dim methodOrPropertySymbolDeclaringReferences = methodOrPropertySymbol.DeclaringSyntaxReferences

            For Each syntaxReference In methodOrPropertySymbolDeclaringReferences
                Dim methodOrPropertyDefinition = syntaxReference.GetSyntax()
                Dim propertyDeclaration = TryCast(methodOrPropertyDefinition, PropertyStatementSyntax)
                If propertyDeclaration IsNot Nothing Then
                    Dim asClause = propertyDeclaration.AsClause
                    Dim retNode = TryGetNodeFromAsClauseForMethodOrProperty(asClause, previewReturnTypeSymbol)
                    If retNode IsNot Nothing Then
                        Return retNode
                    End If
                End If

                Dim methodDeclaration = TryCast(methodOrPropertyDefinition, MethodStatementSyntax)
                If methodDeclaration IsNot Nothing Then
                    Dim asClause = methodDeclaration.AsClause
                    Dim retNode = TryGetNodeFromAsClauseForMethodOrProperty(asClause, previewReturnTypeSymbol)
                    If retNode IsNot Nothing Then
                        Return retNode
                    End If
                End If
            Next

            Return Nothing
        End Function

        Private Function GetSyntaxNodeFromImplementsClause(implementsClause As ImplementsClauseSyntax, previewSymbol As ISymbol) As SyntaxNode
            For Each parameter In implementsClause.InterfaceMembers
                Dim interfacePart = TryCast(parameter.Left, IdentifierNameSyntax)
                If interfacePart IsNot Nothing Then
                    If IsSyntaxToken(interfacePart.Identifier, previewSymbol) Then
                        Return interfacePart
                    End If
                End If

                Dim methodPart = TryCast(parameter.Right, IdentifierNameSyntax)
                If methodPart IsNot Nothing Then
                    If IsSyntaxToken(methodPart.Identifier, previewSymbol) Then
                        Return methodPart
                    End If
                End If
            Next

            Return Nothing
        End Function

        Protected Overrides Function GetPreviewImplementsClauseSyntaxNodeForMethodOrProperty(methodOrPropertySymbol As ISymbol, previewSymbol As ISymbol) As SyntaxNode
            Dim methodSymbolDeclaringReferences = methodOrPropertySymbol.DeclaringSyntaxReferences

            For Each syntaxReference In methodSymbolDeclaringReferences
                Dim methodOrPropertyDefinition = syntaxReference.GetSyntax()
                Dim methodDeclaration = TryCast(methodOrPropertyDefinition, MethodStatementSyntax)
                If methodDeclaration IsNot Nothing Then
                    Dim node = GetSyntaxNodeFromImplementsClause(methodDeclaration.ImplementsClause, previewSymbol)
                    If node IsNot Nothing Then
                        Return node
                    End If
                End If

                Dim propertyDeclaration = TryCast(methodOrPropertyDefinition, PropertyStatementSyntax)
                If propertyDeclaration IsNot Nothing Then
                    Return GetSyntaxNodeFromImplementsClause(propertyDeclaration.ImplementsClause, previewSymbol)
                End If
            Next

            Return Nothing
        End Function

        Protected Overrides Function GetPreviewParameterSyntaxNodeForMethod(methodSymbol As IMethodSymbol, parameterSymbol As ISymbol) As SyntaxNode
            Dim methodSymbolDeclaringReferences = methodSymbol.DeclaringSyntaxReferences

            For Each syntaxReference In methodSymbolDeclaringReferences
                Dim methodDefinition = syntaxReference.GetSyntax()
                Dim methodDeclaration = TryCast(methodDefinition, MethodStatementSyntax)
                If methodDeclaration IsNot Nothing Then
                    Dim parameters = methodDeclaration.ParameterList
                    For Each parameter In parameters.Parameters
                        Dim asClause = parameter.AsClause
                        Dim retNode = TryGetNodeFromAsClauseForMethodOrProperty(asClause, parameterSymbol)
                        If retNode IsNot Nothing Then
                            Return retNode
                        End If
                    Next
                End If

                Dim setAccessorStatement = TryCast(methodDefinition, AccessorStatementSyntax)
                If setAccessorStatement IsNot Nothing Then
                    Dim parameters = setAccessorStatement.ParameterList
                    For Each parameter In parameters.Parameters
                        Dim asClause = parameter.AsClause
                        Dim retNode = TryGetNodeFromAsClauseForMethodOrProperty(asClause, parameterSymbol)
                        If retNode IsNot Nothing Then
                            Return retNode
                        End If
                    Next
                End If
            Next

            Return Nothing
        End Function

        Protected Overrides Function GetPreviewSyntaxNodeForFieldsOrEvents(fieldOrEventSymbol As ISymbol, previewSymbol As ISymbol) As SyntaxNode
            Dim fieldOrEventReferences = fieldOrEventSymbol.DeclaringSyntaxReferences

            For Each fieldOrEventReference In fieldOrEventReferences
                Dim definition = fieldOrEventReference.GetSyntax()

                Dim declaration = TryCast(definition.Parent, VariableDeclaratorSyntax)
                If declaration IsNot Nothing Then
                    Dim asClause = declaration.AsClause
                    Dim retNode = TryGetNodeFromAsClauseForMethodOrProperty(asClause, previewSymbol)
                    If retNode IsNot Nothing Then
                        Return retNode
                    End If
                End If
            Next

            Return Nothing
        End Function
    End Class

End Namespace