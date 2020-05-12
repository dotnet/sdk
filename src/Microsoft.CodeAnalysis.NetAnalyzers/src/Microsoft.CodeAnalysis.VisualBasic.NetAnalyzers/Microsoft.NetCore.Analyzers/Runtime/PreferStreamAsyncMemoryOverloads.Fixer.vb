' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    <ExportCodeFixProvider(LanguageNames.VisualBasic)>
    Public NotInheritable Class PreferStreamAsyncMemoryOverloadsVisualBasicFixer

        Inherits PreferStreamAsyncMemoryOverloadsFixer

        Protected Overrides Function GetArgumentByPositionOrName(args As ImmutableArray(Of IArgumentOperation), index As Integer, name As String, ByRef isNamed As Boolean) As IArgumentOperation
            isNamed = False
            If index >= args.Length Then
                Return Nothing
            Else
                Dim argNode = TryCast(args(index).Syntax, SimpleArgumentSyntax)
                If argNode IsNot Nothing AndAlso argNode.NameColonEquals Is Nothing Then
                    Return args(index)
                Else
                    isNamed = True
                    Return args.FirstOrDefault(
                        Function(argOperation)
                            argNode = TryCast(argOperation.Syntax, SimpleArgumentSyntax)
                            Return argNode.NameColonEquals?.Name?.Identifier.ValueText = name
                        End Function)
                End If
            End If
        End Function
    End Class
End Namespace
