' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.NetCore.Analyzers.Runtime
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicUseSpanBasedStringConcat : Inherits UseSpanBasedStringConcat

        Private Protected Overrides Function IsTopMostConcatOperation(binaryOperation As IBinaryOperation) As Boolean
            If Not IsStringConcatOperation(binaryOperation) Then
                Return False
            End If

            Dim parentBinaryOperation = TryCast(WalkUpImplicitConversionToObject(binaryOperation.Parent), IBinaryOperation)
            Return parentBinaryOperation Is Nothing OrElse Not IsStringConcatOperation(parentBinaryOperation)
        End Function

        Private Protected Overrides Function WalkDownBuiltInImplicitConversionOnConcatOperand(operand As IOperation) As IOperation

            Return BasicWalkDownBuiltInImplicitConversionOnConcatOperand(operand)
        End Function

        Private Shared Function IsStringConcatOperation(operation As IBinaryOperation) As Boolean

            'OperatorKind will be Concatenate even when the "+" operator is used, provided both operands are strings.
            Return operation.OperatorKind = BinaryOperatorKind.Concatenate
        End Function

        Private Shared Function WalkUpImplicitConversionToObject(operation As IOperation) As IOperation

            Dim conversion = TryCast(operation, IConversionOperation)
            If conversion IsNot Nothing AndAlso conversion.Type.SpecialType = SpecialType.System_Object AndAlso
                conversion.IsImplicit AndAlso Not conversion.Conversion.IsUserDefined Then
                Return conversion.Parent
            Else
                Return operation
            End If
        End Function
    End Class
End Namespace

