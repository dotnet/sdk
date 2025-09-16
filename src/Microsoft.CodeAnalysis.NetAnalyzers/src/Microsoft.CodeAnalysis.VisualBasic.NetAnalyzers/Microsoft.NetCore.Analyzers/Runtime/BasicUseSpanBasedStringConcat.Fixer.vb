﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    <ExportCodeFixProvider(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicUseSpanBasedStringConcatFixer : Inherits UseSpanBasedStringConcatFixer

        Private Protected Overrides Function ReplaceInvocationMethodName(generator As SyntaxGenerator, invocationSyntax As SyntaxNode, newName As String) As SyntaxNode

            Dim cast = DirectCast(invocationSyntax, InvocationExpressionSyntax)
            Dim memberAccessSyntax = DirectCast(cast.Expression, MemberAccessExpressionSyntax)
            Dim oldNameSyntax = memberAccessSyntax.Name
            Dim newNameSyntax = generator.IdentifierName(newName).WithTriviaFrom(oldNameSyntax)
            Return invocationSyntax.ReplaceNode(oldNameSyntax, newNameSyntax)
        End Function

        Private Protected Overrides Function WalkDownBuiltInImplicitConversionOnConcatOperand(operand As IOperation) As IOperation

            Return UseSpanBasedStringConcat.BasicWalkDownBuiltInImplicitConversionOnConcatOperand(operand)
        End Function

        Private Protected Overrides Function IsNamedArgument(argumentOperation As IArgumentOperation) As Boolean

            Dim argumentSyntax = TryCast(argumentOperation.Syntax, ArgumentSyntax)
            Return argumentSyntax IsNot Nothing AndAlso argumentSyntax.IsNamed
        End Function
    End Class
End Namespace

