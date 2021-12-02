' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines
    Friend Class BasicAvoidMultipleEnumerationsAnalyzer
        Private Class BasicAvoidMultipleEnumerationsHelpers
            Inherits AvoidMultipleEnumerationsHelpers

            Public Shared ReadOnly Instance As New BasicAvoidMultipleEnumerationsHelpers()

            Public Overrides Function IsDeferredExecutingInvocationOverInvocationInstance(invocationOperation As IInvocationOperation, wellKnownSymbolsInfo As WellKnownSymbolsInfo) As Boolean
                If invocationOperation.Instance Is Nothing OrElse invocationOperation.TargetMethod.MethodKind <> MethodKind.ReducedExtension Then
                    Return False
                End If

                If Not IsDeferredType(invocationOperation.Instance.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes) Then
                    Return False
                End If

                Dim originalTargetMethod = invocationOperation.TargetMethod.ReducedFrom.OriginalDefinition
                Return wellKnownSymbolsInfo.OneParameterDeferredMethods.Contains(originalTargetMethod) OrElse
                    wellKnownSymbolsInfo.TwoParametersDeferredMethods.Contains(originalTargetMethod)
            End Function

            Protected Overrides Function IsInvocationCausingEnumerationOverInvocationInstance(invocationOperation As IInvocationOperation, wellKnownSymbolsInfo As WellKnownSymbolsInfo) As Boolean
                If invocationOperation.Instance Is Nothing OrElse invocationOperation.TargetMethod.MethodKind <> MethodKind.ReducedExtension Then
                    Return False
                End If

                If Not IsDeferredType(invocationOperation.Instance.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes) Then
                    Return False
                End If

                Dim originalTargetMethod = invocationOperation.TargetMethod.ReducedFrom.OriginalDefinition
                Return wellKnownSymbolsInfo.OneParameterEnumeratedMethods.Contains(originalTargetMethod) OrElse
                    wellKnownSymbolsInfo.TwoParametersEnumeratedMethods.Contains(originalTargetMethod) OrElse
                    (Not originalTargetMethod.Parameters.IsEmpty AndAlso HasDeferredTypeConstraint(originalTargetMethod.Parameters(0), wellKnownSymbolsInfo))
            End Function

            Protected Overrides Function IsOperationTheInstanceOfDeferredInvocation(operation As IOperation, wellKnownSymbolsInfo As WellKnownSymbolsInfo) As Boolean
                Dim parentInvocationOperation = TryCast(operation.Parent, IInvocationOperation)
                Return parentInvocationOperation IsNot Nothing AndAlso IsDeferredExecutingInvocationOverInvocationInstance(parentInvocationOperation, wellKnownSymbolsInfo)
            End Function
        End Class
    End Class
End Namespace