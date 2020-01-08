' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.NetCore.Analyzers.Performance

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Performance

    ''' <summary>
    ''' CA1829: Visual Basic implementation Of use Property instead Of <see cref="Enumerable.Count(Of TSource)(IEnumerable(Of TSource))"/>, When available.
    ''' </summary>
    ''' <remarks>
    ''' Flags the use of <see cref="Enumerable.Count(Of TSource)(IEnumerable(Of TSource))"/> on types that are know to have a property with the same semantics:
    ''' <c>Length</c>, <c>Count</c>.
    ''' </remarks>
    ''' <seealso cref="UsePropertyInsteadOfCountMethodWhenAvailableAnalyzer"/>
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicUsePropertyInsteadOfCountMethodWhenAvailableAnalyzer
        Inherits UsePropertyInsteadOfCountMethodWhenAvailableAnalyzer

        ''' <summary>
        ''' Creates the operation actions handler.
        ''' </summary>
        ''' <param name="context">The context.</param>
        ''' <returns>The operation actions handler.</returns>
        Protected Overrides Function CreateOperationActionsHandler(context As OperationActionsContext) As OperationActionsHandler

            Return New BasicOperationActionsHandler(context)

        End Function

        ''' <summary>
        ''' Handler for operaction actions for Visual Basic. This class cannot be inherited.
        ''' Implements the <see cref="Microsoft.NetCore.Analyzers.Performance.UsePropertyInsteadOfCountMethodWhenAvailableAnalyzer.OperationActionsHandler" />
        ''' </summary>
        ''' <seealso cref="Microsoft.NetCore.Analyzers.Performance.UsePropertyInsteadOfCountMethodWhenAvailableAnalyzer.OperationActionsHandler" />
        Private NotInheritable Class BasicOperationActionsHandler
            Inherits OperationActionsHandler

            ''' <summary>
            ''' Initializes a new instance of the <see cref="BasicOperationActionsHandler"/> class.
            ''' </summary>
            ''' <param name="context">The context.</param>
            Public Sub New(context As OperationActionsContext)
                MyBase.New(context)
            End Sub

            ''' <summary>
            ''' Gets the type of the receiver of the <see cref="System.Linq.Enumerable.Count(Of TSource)(IEnumerable(Of TSource))" />.
            ''' </summary>
            ''' <param name="invocationOperation">The invocation operation.</param>
            ''' <returns>The <see cref="ITypeSymbol" /> of the receiver of the extension method.</returns>
            Protected Overrides Function GetEnumerableCountInvocationTargetType(invocationOperation As IInvocationOperation) As ITypeSymbol

                Dim method = invocationOperation.TargetMethod

                If invocationOperation.Arguments.Length = 0 AndAlso
                    method.Name.Equals(NameOf(Enumerable.Count), StringComparison.Ordinal) AndAlso
                    Me.Context.IsEnumerableType(method.ContainingSymbol) Then

                    Dim convertionOperation = TryCast(invocationOperation.Instance, IConversionOperation)

                    Return If(Not convertionOperation Is Nothing,
                        convertionOperation.Operand.Type,
                        invocationOperation.Instance.Type)


                End If

                Return Nothing

            End Function
        End Class

    End Class

End Namespace
