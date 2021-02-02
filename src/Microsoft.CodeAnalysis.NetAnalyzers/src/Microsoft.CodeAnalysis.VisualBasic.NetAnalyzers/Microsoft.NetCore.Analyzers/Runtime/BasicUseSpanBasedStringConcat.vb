' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.NetCore.Analyzers.Runtime
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicUseSpanBasedStringConcat : Inherits UseSpanBasedStringConcat

        Private Protected Overrides Function TryGetTopMostConcatOperation(binaryOperation As IBinaryOperation, ByRef rootBinaryOperation As IBinaryOperation) As Boolean

            If Not IsStringConcatOperation(binaryOperation) Then
                rootBinaryOperation = Nothing
                Return False
            End If

            Dim parentBinaryOperation = binaryOperation
            Dim current As IBinaryOperation
            Do
                current = parentBinaryOperation
                parentBinaryOperation = TryCast(current.Parent, IBinaryOperation)
            Loop While parentBinaryOperation IsNot Nothing AndAlso IsStringConcatOperation(parentBinaryOperation)

            rootBinaryOperation = current
            Return True
        End Function

        Private Shared Function IsStringConcatOperation(operation As IBinaryOperation) As Boolean

            'OperatorKind will be Concatenate even when the "+" operator is used, provided both operands are strings.
            Return operation.OperatorKind = BinaryOperatorKind.Concatenate
        End Function
    End Class
End Namespace

