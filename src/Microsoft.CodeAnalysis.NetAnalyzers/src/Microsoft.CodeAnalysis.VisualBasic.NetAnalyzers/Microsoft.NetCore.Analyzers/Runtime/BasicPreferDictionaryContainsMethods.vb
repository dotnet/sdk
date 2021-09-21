' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicPreferDictionaryContainsMethods : Inherits PreferDictionaryContainsMethods

        Private Protected Overrides Function TryGetPropertyReferenceOperation(containsInvocation As IInvocationOperation, ByRef propertySymbol As IPropertySymbol) As Boolean

            Dim method = containsInvocation.TargetMethod
            Dim receiver As IOperation = Nothing

            If method.Parameters.Length = 1 Then
                receiver = containsInvocation.Instance
            End If

            Dim receiverAsConversion = TryCast(receiver, IConversionOperation)
            If receiverAsConversion IsNot Nothing Then
                receiver = receiverAsConversion.Operand
            End If

            Dim receiverAsPropertyReference = TryCast(receiver, IPropertyReferenceOperation)
            If receiverAsPropertyReference IsNot Nothing Then
                propertySymbol = receiverAsPropertyReference.Property
            Else
                propertySymbol = Nothing
            End If

            Return propertySymbol IsNot Nothing
        End Function
    End Class
End Namespace
