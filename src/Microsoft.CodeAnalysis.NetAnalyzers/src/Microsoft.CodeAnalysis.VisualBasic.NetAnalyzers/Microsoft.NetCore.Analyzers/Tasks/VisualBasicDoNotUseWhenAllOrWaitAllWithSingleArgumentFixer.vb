' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Tasks

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Tasks

    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class VisualBasicDoNotUseWhenAllOrWaitAllWithSingleArgumentFixer
        Inherits DoNotUseWhenAllOrWaitAllWithSingleArgumentFixer

        Protected Overrides Function GetSingleArgumentSyntax(operation As IInvocationOperation) As SyntaxNode
            Dim invocationSyntax = DirectCast(operation.Syntax, InvocationExpressionSyntax)
            Return invocationSyntax.ArgumentList.Arguments.Single().GetExpression()
        End Function
    End Class
End Namespace
