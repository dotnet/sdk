' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Simplification
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    Partial Public NotInheritable Class BasicForwardCancellationTokenToInvocationsFixer
        Private Class TypeNameVisitor
            Inherits SymbolVisitor(Of TypeSyntax)

            Public Shared Function GetTypeSyntaxForSymbol(symbol As INamespaceOrTypeSymbol) As TypeSyntax
                Return symbol.Accept(New TypeNameVisitor()).WithAdditionalAnnotations(Simplifier.Annotation)
            End Function

            Public Overrides Function DefaultVisit(symbol As ISymbol) As TypeSyntax
                Throw New NotImplementedException()
            End Function

            Public Overrides Function VisitAlias(symbol As IAliasSymbol) As TypeSyntax
                Return AddInformationTo(ToIdentifierName(symbol.Name))
            End Function

            Public Overrides Function VisitArrayType(symbol As IArrayTypeSymbol) As TypeSyntax
                Dim underlyingNonArrayType = symbol.ElementType
                While underlyingNonArrayType.Kind = SymbolKind.ArrayType
                    underlyingNonArrayType = DirectCast(underlyingNonArrayType, IArrayTypeSymbol).ElementType
                End While

                Dim elementTypeSyntax = underlyingNonArrayType.Accept(Me)
                Dim ranks = New List(Of ArrayRankSpecifierSyntax)()
                Dim arrayType = symbol
                While arrayType IsNot Nothing
                    Dim commaCount = Math.Max(0, arrayType.Rank - 1)
                    Dim commas = SyntaxFactory.TokenList(Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken), commaCount))
                    ranks.Add(SyntaxFactory.ArrayRankSpecifier(SyntaxFactory.Token(SyntaxKind.OpenParenToken), commas, SyntaxFactory.Token(SyntaxKind.CloseParenToken)))
                    arrayType = TryCast(arrayType.ElementType, IArrayTypeSymbol)
                End While

                Dim arrayTypeSyntax = SyntaxFactory.ArrayType(elementTypeSyntax, SyntaxFactory.List(ranks))
                Return AddInformationTo(arrayTypeSyntax)
            End Function

            Public Overrides Function VisitDynamicType(symbol As IDynamicTypeSymbol) As TypeSyntax
                Return AddInformationTo(SyntaxFactory.IdentifierName("dynamic"))
            End Function

            Public Overrides Function VisitNamedType(symbol As INamedTypeSymbol) As TypeSyntax
                Dim typeSyntax = CreateSimpleTypeSyntax(symbol)
                If Not (TypeOf typeSyntax Is SimpleNameSyntax) Then
                    Return typeSyntax
                End If

                Dim simpleNameSyntax = DirectCast(typeSyntax, SimpleNameSyntax)
                If symbol.ContainingType IsNot Nothing Then
                    If symbol.ContainingType.TypeKind = TypeKind.Submission Then
                        Return typeSyntax
                    Else
                        Return AddInformationTo(SyntaxFactory.QualifiedName(DirectCast(symbol.ContainingType.Accept(Me), NameSyntax), simpleNameSyntax))
                    End If
                ElseIf symbol.ContainingNamespace IsNot Nothing Then
                    If symbol.ContainingNamespace.IsGlobalNamespace Then
                        If symbol.TypeKind <> TypeKind.[Error] Then
                            Return AddInformationTo(SyntaxFactory.QualifiedName(SyntaxFactory.GlobalName(), simpleNameSyntax))
                        End If
                    Else
                        Dim container = symbol.ContainingNamespace.Accept(Me)
                        Return AddInformationTo(SyntaxFactory.QualifiedName(DirectCast(container, NameSyntax), simpleNameSyntax))
                    End If
                End If

                Return simpleNameSyntax
            End Function

            Public Overrides Function VisitNamespace(symbol As INamespaceSymbol) As TypeSyntax
                Dim result = AddInformationTo(ToIdentifierName(symbol.Name))
                If symbol.ContainingNamespace Is Nothing Then
                    Return result
                End If

                If symbol.ContainingNamespace.IsGlobalNamespace Then
                    Return AddInformationTo(SyntaxFactory.QualifiedName(SyntaxFactory.GlobalName(), result))
                Else
                    Dim container = symbol.ContainingNamespace.Accept(Me)
                    Return AddInformationTo(SyntaxFactory.QualifiedName(DirectCast(container, NameSyntax), result))
                End If
            End Function

            Public Overrides Function VisitPointerType(symbol As IPointerTypeSymbol) As TypeSyntax
                Return symbol.PointedAtType.Accept(Me)
            End Function

            Public Overrides Function VisitTypeParameter(symbol As ITypeParameterSymbol) As TypeSyntax
                Return AddInformationTo(ToIdentifierName(symbol.Name))
            End Function

            Private Shared Function AddInformationTo(Of TTypeSyntax As TypeSyntax)(type As TTypeSyntax) As TTypeSyntax
                type = type.WithLeadingTrivia(SyntaxFactory.ElasticMarker).WithTrailingTrivia(SyntaxFactory.ElasticMarker)
                Return type
            End Function

            Private Shared Function CreateSimpleTypeSyntax(symbol As INamedTypeSymbol) As TypeSyntax
                Dim syntax = TryCreateSpecializedNamedTypeSyntax(symbol)
                If syntax IsNot Nothing Then
                    Return syntax
                End If

                If symbol.IsTupleType AndAlso symbol.TupleUnderlyingType IsNot Nothing AndAlso Not symbol.Equals(symbol.TupleUnderlyingType) Then
                    Return CreateSimpleTypeSyntax(symbol.TupleUnderlyingType)
                End If

                If symbol.Name = String.Empty OrElse symbol.IsAnonymousType Then
                    Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("Object"))
                End If

                If symbol.OriginalDefinition.SpecialType = SpecialType.System_Nullable_T Then
                    Return AddInformationTo(SyntaxFactory.NullableType(symbol.TypeArguments.First().Accept(New TypeNameVisitor())))
                End If

                If symbol.TypeParameters.Length = 0 Then
                    Return ToIdentifierName(symbol.Name)
                End If

                Return SyntaxFactory.GenericName(
                ToIdentifierToken(symbol.Name),
                SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(symbol.TypeArguments.[Select](Function(t) t.Accept(New TypeNameVisitor())))))
            End Function

            Private Shared Function TryCreateSpecializedNamedTypeSyntax(symbol As INamedTypeSymbol) As TypeSyntax
                Select Case symbol.SpecialType
                    Case SpecialType.System_Object
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("Object"))
                    Case SpecialType.System_Boolean
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("Boolean"))
                    Case SpecialType.System_Char
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("Char"))
                    Case SpecialType.System_SByte
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("SByte"))
                    Case SpecialType.System_Byte
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("Byte"))
                    Case SpecialType.System_Int16
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("Int16"))
                    Case SpecialType.System_UInt16
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("UInt16"))
                    Case SpecialType.System_Int32
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("Int32"))
                    Case SpecialType.System_Int64
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("Int64"))
                    Case SpecialType.System_UInt32
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("UInt32"))
                    Case SpecialType.System_UInt64
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("UInt64"))
                    Case SpecialType.System_Decimal
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("Decimal"))
                    Case SpecialType.System_Single
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("Single"))
                    Case SpecialType.System_Double
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("Double"))
                    Case SpecialType.System_String
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("String"))
                    Case SpecialType.System_DateTime
                        Return SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("System"), SyntaxFactory.IdentifierName("DateTime"))
                End Select

                If symbol.IsTupleType AndAlso symbol.TupleElements.Length >= 2 Then
                    Return CreateTupleTypeSyntax(symbol)
                End If

                Return Nothing
            End Function

            Private Shared Function CreateTupleTypeSyntax(symbol As INamedTypeSymbol) As TypeSyntax
                Dim elements = symbol.TupleElements

                Return SyntaxFactory.TupleType(SyntaxFactory.SeparatedList(
                elements.Select(Function(element) If(Not element.IsImplicitlyDeclared,
                                                        SyntaxFactory.NamedTupleElement(
                                                                        SyntaxFactory.Identifier(element.Name),
                                                                        SyntaxFactory.SimpleAsClause(
                                                                                    SyntaxFactory.Token(SyntaxKind.AsKeyword),
                                                                                    Nothing,
                                                                                    GetTypeSyntaxForSymbol(element.Type))),
                                                        DirectCast(SyntaxFactory.TypedTupleElement(
                                                                        GetTypeSyntaxForSymbol(element.Type)), TupleElementSyntax)))))
            End Function

            Private Shared Function ToIdentifierName(text As String) As IdentifierNameSyntax
                Return SyntaxFactory.IdentifierName(ToIdentifierToken(text)).WithAdditionalAnnotations(Simplifier.Annotation)
            End Function

            Private Shared Function ToIdentifierToken(text As String, Optional afterDot As Boolean = False, Optional symbol As ISymbol = Nothing, Optional withinAsyncMethod As Boolean = False) As SyntaxToken

                Dim unescaped = text
                Dim wasAlreadyEscaped = False

                If text.Length > 2 AndAlso MakeHalfWidthIdentifier(text.First()) = "[" AndAlso MakeHalfWidthIdentifier(text.Last()) = "]" Then
                    unescaped = text.Substring(1, text.Length() - 2)
                    wasAlreadyEscaped = True
                End If

                Dim escaped = EscapeIdentifier(text, afterDot, symbol, withinAsyncMethod)
                Dim token = If(escaped.Length > 0 AndAlso escaped(0) = "["c,
                    SyntaxFactory.Identifier(escaped, isBracketed:=True, identifierText:=unescaped, typeCharacter:=TypeCharacter.None),
                    SyntaxFactory.Identifier(text))

                If Not wasAlreadyEscaped Then
                    token = token.WithAdditionalAnnotations(Simplifier.Annotation)
                End If

                Return token
            End Function

            Private Shared Function EscapeIdentifier(text As String, Optional afterDot As Boolean = False, Optional symbol As ISymbol = Nothing, Optional withinAsyncMethod As Boolean = False) As String
                Dim keywordKind = SyntaxFacts.GetKeywordKind(text)
                Dim needsEscaping = keywordKind <> SyntaxKind.None

                ' REM and New must always be escaped, but there are some conditions where
                ' keywords are not escaped
                If needsEscaping AndAlso
                keywordKind <> SyntaxKind.REMKeyword AndAlso
                keywordKind <> SyntaxKind.NewKeyword Then

                    needsEscaping = Not afterDot

                    If needsEscaping Then
                        Dim typeSymbol = TryCast(symbol, ITypeSymbol)
                        needsEscaping = typeSymbol Is Nothing OrElse Not IsPredefinedType(typeSymbol)
                    End If
                End If

                ' GetKeywordKind won't return SyntaxKind.AwaitKeyword (943836)
                If withinAsyncMethod AndAlso text = "Await" Then
                    needsEscaping = True
                End If

                Return If(needsEscaping, "[" & text & "]", text)
            End Function

            Private Shared Function IsPredefinedType(type As ITypeSymbol) As Boolean
                Select Case type.SpecialType
                    Case SpecialType.System_Boolean,
                     SpecialType.System_Byte,
                     SpecialType.System_SByte,
                     SpecialType.System_Int16,
                     SpecialType.System_UInt16,
                     SpecialType.System_Int32,
                     SpecialType.System_UInt32,
                     SpecialType.System_Int64,
                     SpecialType.System_UInt64,
                     SpecialType.System_Single,
                     SpecialType.System_Double,
                     SpecialType.System_Decimal,
                     SpecialType.System_DateTime,
                     SpecialType.System_Char,
                     SpecialType.System_String,
                     SpecialType.System_Object
                        Return True
                    Case Else
                        Return False
                End Select
            End Function

            ''' <summary>
            ''' Creates a half width form Unicode character string. 
            ''' </summary>
            ''' <param name="text">The text representing the original identifier.  This can be in full width or half width Unicode form.  </param>
            ''' <returns>A string representing the text in a half width Unicode form.</returns>
            Private Shared Function MakeHalfWidthIdentifier(text As String) As String
                If text Is Nothing Then
                    Return text
                End If

                Dim characters As Char() = Nothing
                For i = 0 To text.Length - 1
                    Dim c = text(i)

                    If IsFullWidth(c) Then
                        If characters Is Nothing Then
                            characters = New Char(text.Length - 1) {}
                            text.CopyTo(0, characters, 0, i)
                        End If

                        characters(i) = MakeHalfWidth(c)
                    ElseIf characters IsNot Nothing Then
                        characters(i) = c
                    End If
                Next

                Return If(characters Is Nothing, text, New String(characters))
            End Function

            Private Const s_fullwidth = &HFF00L - &H20L

            '// IsFullWidth - Returns if the character is full width
            Private Shared Function IsFullWidth(c As Char) As Boolean
                ' Do not use "AndAlso" or it will not inline.
                Return c > ChrW(&HFF00US) And c < ChrW(&HFF5FUS)
            End Function

            '// MakeHalfWidth - Converts a full-width character to half-width
            Friend Shared Function MakeHalfWidth(c As Char) As Char
                Debug.Assert(IsFullWidth(c))

                Return Convert.ToChar(Convert.ToUInt16(c) - s_fullwidth)
            End Function
        End Class
    End Class
End Namespace
