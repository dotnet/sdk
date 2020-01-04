// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    /// <summary>
    /// CA1829: C# implementation of use property instead of <see cref="Enumerable.Count{TSource}(System.Collections.Generic.IEnumerable{TSource})"/>, when available.
    /// Implements the <see cref="UsePropertyInsteadOfCountMethodWhenAvailableAnalyzer" />
    /// </summary>
    /// <remarks>
    /// Flags the use of <see cref="Enumerable.Count{TSource}(System.Collections.Generic.IEnumerable{TSource})"/> on types that are know to have a property with the same semantics:
    /// <c>Length</c>, <c>Count</c>.
    /// </remarks>
    /// <seealso cref="UsePropertyInsteadOfCountMethodWhenAvailableAnalyzer"/>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpUsePropertyInsteadOfCountMethodWhenAvailableAnalyzer
        : UsePropertyInsteadOfCountMethodWhenAvailableAnalyzer
    {
        /// <summary>
        /// Creates the operation actions handler.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The operation actions handler.</returns>
        protected override OperationActionsHandler CreateOperationActionsHandler(OperationActionsContext context)
            => new CSharpOperationActionsHandler(context);

        /// <summary>
        /// Handler for operaction actions for C#. This class cannot be inherited.
        /// Implements the <see cref="Microsoft.NetCore.Analyzers.Performance.UsePropertyInsteadOfCountMethodWhenAvailableAnalyzer.OperationActionsHandler" />
        /// </summary>
        /// <seealso cref="Microsoft.NetCore.Analyzers.Performance.UsePropertyInsteadOfCountMethodWhenAvailableAnalyzer.OperationActionsHandler" />
        private sealed class CSharpOperationActionsHandler : OperationActionsHandler
        {
            internal CSharpOperationActionsHandler(OperationActionsContext context)
                : base(context)
            {
            }

            protected override ITypeSymbol? GetEnumerableCountInvocationTargetType(IInvocationOperation invocationOperation)
            {
                var method = invocationOperation.TargetMethod;

                if (invocationOperation.Arguments.Length == 1 &&
                    method.Name.Equals(nameof(Enumerable.Count), StringComparison.Ordinal) &&
                    this.Context.IsEnumerableType(method.ContainingSymbol))
                {
                    return invocationOperation.Arguments[0].Value is IConversionOperation convertionOperation
                        ? convertionOperation.Operand.Type
                        : invocationOperation.Arguments[0].Value.Type;
                }

                return null;
            }
        }
    }
}
