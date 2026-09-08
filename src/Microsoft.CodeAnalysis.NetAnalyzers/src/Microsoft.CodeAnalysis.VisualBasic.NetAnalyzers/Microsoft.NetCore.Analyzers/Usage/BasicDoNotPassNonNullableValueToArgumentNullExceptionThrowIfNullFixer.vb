' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.
Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Usage

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Usage
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicDoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullFixer
        Inherits DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullFixer(Of InvocationExpressionSyntax)

        Protected Overrides Sub ReplaceWithNullableStructCheck(invocation As InvocationExpressionSyntax, statement As SyntaxNode, editor As SyntaxEditor)
            Dim generator = editor.Generator
            Dim nullableStructExpression = invocation.ArgumentList.Arguments(0).GetExpression()
            Dim condition = generator.LogicalNotExpression(generator.MemberAccessExpression(nullableStructExpression, HasValue))
            Dim nameOfExpression = generator.NameOfExpression(nullableStructExpression)
            Dim argumentNullEx = generator.ObjectCreationExpression(generator.IdentifierName(ArgumentNullException), nameOfExpression)
            Dim throwExpression = generator.ThrowStatement(argumentNullEx)
            editor.ReplaceNode(statement, generator.IfStatement(condition, New SyntaxNode() {throwExpression}))
        End Sub
    End Class
End Namespace