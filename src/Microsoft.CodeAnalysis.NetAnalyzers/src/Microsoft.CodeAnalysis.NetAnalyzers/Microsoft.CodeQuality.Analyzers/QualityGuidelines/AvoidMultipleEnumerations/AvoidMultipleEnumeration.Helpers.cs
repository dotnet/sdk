// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public sealed partial class AvoidMultipleEnumerations
    {
        private static bool IsParameterOrLocalEnumerated(
            IOperation parameterOrLocalReferenceOperation,
            ImmutableArray<IMethodSymbol> wellKnownDelayExecutingMethods,
            ImmutableArray<IMethodSymbol> wellKnownEnumerationMethods)
        {
            RoslynDebug.Assert(parameterOrLocalReferenceOperation is ILocalReferenceOperation or IParameterReferenceOperation);

            // 1. ForEach Loop that enumerate local or parameter
            if (IsTheExpressionOfForEachLoop(parameterOrLocalReferenceOperation))
            {
                return true;
            }

            // 2. Used as argument to other invocation operation
            // e.g.
            // var i = c.First();
            if (parameterOrLocalReferenceOperation.Parent is IArgumentOperation { Parent: IInvocationOperation invocationOperation } argumentOperation)
            {
                // Skip the linq chain in the middle if needed.
                // e.g.
                // c.Select(i => i + 1).Where(i != 10).First();
                // Go to the Where() invocation.
                var lastDelayExecutingInvocation = GetLastDelayExecutingInvocation(argumentOperation, invocationOperation, wellKnownDelayExecutingMethods);
                if (lastDelayExecutingInvocation != null)
                {
                    // Two cases here:
                    // I:
                    // Local or Parameter that are involved in a linq chain method, then invoked in a for each loop
                    // e.g.
                    // foreach (var i in c.Select(h => h + 1).Where(k => k != 10)) { }
                    // II:
                    // It is invoked in the linq chain end with Enumeration Method
                    // e.g.
                    // c.Select(i => i + 1).Where(i != 10).First();
                    return IsTheExpressionOfForEachLoop(lastDelayExecutingInvocation)
                           || IsInvocationEnumeratedByParent(lastDelayExecutingInvocation, wellKnownEnumerationMethods);
                }

                // No linq chain, just check the argument
                return IsInvocationEnumeratedByParent(parameterOrLocalReferenceOperation, wellKnownEnumerationMethods);
            }

            return false;
        }

        private static bool IsTheExpressionOfForEachLoop(IOperation operation)
        {
            // ForEach loop would convert all the expression to IEnumerable<T> before call the GetEnumerator method.
            // So the expression would be wrapped with a ConversionOperation
            return operation.Parent is IConversionOperation { Parent: IForEachLoopOperation };
        }

        private static bool IsInvocationEnumeratedByParent(IOperation operation, ImmutableArray<IMethodSymbol> wellKnownEnumerationMethods)
        {
            if (operation.Parent is IArgumentOperation { Parent: IInvocationOperation invocationOperation } argumentOperation)
            {
                return IsInvocationCausingEnumerationOverArgument(invocationOperation, argumentOperation, wellKnownEnumerationMethods);
            }

            return false;
        }

        private static IInvocationOperation? GetLastDelayExecutingInvocation(
            IArgumentOperation argumentOperation,
            IInvocationOperation invocationOperation,
            ImmutableArray<IMethodSymbol> wellKnownDelayExecutingMethods)
        {
            if (IsDelayExecutingInvocation(invocationOperation, argumentOperation, wellKnownDelayExecutingMethods))
            {
                // If the current invocation is delay executing method, and we can walk up the invocation chain, check the parent.
                if (invocationOperation.Parent is IArgumentOperation { Parent: IInvocationOperation parentInvocationOperation } parentArgumentOperation)
                {
                    return GetLastDelayExecutingInvocation(parentArgumentOperation, parentInvocationOperation,wellKnownDelayExecutingMethods);
                }

                // This is the last delay executing invocation
                return invocationOperation;
            }

            return null;
        }

        private static bool IsInvocationCausingEnumerationOverArgument(
            IInvocationOperation invocationOperation,
            IArgumentOperation argumentOperationToCheck,
            ImmutableArray<IMethodSymbol> wellKnownMethodCausingEnumeration)
        {
            RoslynDebug.Assert(invocationOperation.Arguments.Contains(argumentOperationToCheck));

            var targetMethod = invocationOperation.TargetMethod;
            var parameter = argumentOperationToCheck.Parameter;
            if (wellKnownMethodCausingEnumeration.Contains(targetMethod)
                && parameter.Name == "Source"
                && argumentOperationToCheck.Value.Type.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return true;
            }

            // If the method is accepting a parameter that constrained to IEnumerable<>
            // e.g.
            // void Bar<T>(T t) where T : IEnumerable<T> { }
            // Assuming it is going to enumerate the argument
            if (parameter.Type is ITypeParameterSymbol typeParameterSymbol
                && typeParameterSymbol.ConstraintTypes
                    .Any(constraintType => constraintType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T))
            {
                return true;
            }

            // TODO: we might want to have an attribute support here to mark a argument as 'enumerated'

            return false;
        }

        private static bool IsDelayExecutingInvocation(
            IInvocationOperation invocationOperation,
            IArgumentOperation argumentOperationToCheck,
            ImmutableArray<IMethodSymbol> wellKnownDelayExecutingMethods)
        {
            RoslynDebug.Assert(invocationOperation.Arguments.Contains(argumentOperationToCheck));

            var targetMethod = invocationOperation.TargetMethod;
            // TODO: Consider hard code all the linq method case here to make it more accurate
            return wellKnownDelayExecutingMethods.Contains(targetMethod)
                   && argumentOperationToCheck.Parameter.Type.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;
        }
    }
}