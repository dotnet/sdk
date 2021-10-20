// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public partial class AvoidMultipleEnumerations
    {
        private static bool IsOperationEnumeratedByMethodInvocation(
            IOperation operation,
            ImmutableArray<IMethodSymbol> wellKnownDeferredExecutingMethodsTakeOneIEnumerable,
            ImmutableArray<IMethodSymbol> wellKnownDeferredExecutionMethodsTakeTwoIEnumerables,
            ImmutableArray<IMethodSymbol> wellKnownEnumerationMethods)
        {
            RoslynDebug.Assert(operation is ILocalReferenceOperation or IParameterReferenceOperation);
            if (operation.Type.OriginalDefinition.SpecialType != SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return false;
            }

            var operationToCheck = SkipDeferredExecutingMethodIfNeeded(
                operation,
                wellKnownDeferredExecutingMethodsTakeOneIEnumerable,
                wellKnownDeferredExecutionMethodsTakeTwoIEnumerables);
            return IsOperationEnumeratedByInvocation(operationToCheck, wellKnownEnumerationMethods);
        }

        private static IOperation SkipDeferredExecutingMethodIfNeeded(
            IOperation operation,
            ImmutableArray<IMethodSymbol> wellKnownDeferredExecutingMethodsTakeOneIEnumerable,
            ImmutableArray<IMethodSymbol> wellKnownDeferredExecutionMethodsTakeTwoIEnumerables)
        {
            if (operation.Parent is IArgumentOperation { Parent: IInvocationOperation invocationOperation } argumentOperation)
            {
                // Skip the linq chain in the middle if needed.
                // e.g.
                // c.Select(i => i + 1).Where(i != 10).First();
                // Go to the Where() invocation.
                var lastDeferredExecutingInvocation = GetLastDeferredExecutingInvocation(
                    argumentOperation,
                    invocationOperation,
                    wellKnownDeferredExecutingMethodsTakeOneIEnumerable,
                    wellKnownDeferredExecutionMethodsTakeTwoIEnumerables);
                return lastDeferredExecutingInvocation ?? operation;
            }

            return operation;
        }

        private static bool IsOperationEnumeratedByForEachLoop(
            IOperation operation,
            ImmutableArray<IMethodSymbol> wellKnownDeferredExecutingMethodsTakeOneIEnumerable,
            ImmutableArray<IMethodSymbol> wellKnownDeferredExecutionMethodsTakeTwoIEnumerables)
        {
            RoslynDebug.Assert(operation is ILocalReferenceOperation or IParameterReferenceOperation);
            if (operation.Type.OriginalDefinition.SpecialType != SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return false;
            }

            var operationToCheck = SkipDeferredExecutingMethodIfNeeded(operation, wellKnownDeferredExecutingMethodsTakeOneIEnumerable, wellKnownDeferredExecutionMethodsTakeTwoIEnumerables);
            return IsTheExpressionOfForEachLoop(operationToCheck);
        }

        private static bool IsTheExpressionOfForEachLoop(IOperation operation)
        {
            // ForEach loop would convert all the expression to IEnumerable<T> before call the GetEnumerator method.
            // So the expression would be wrapped with a ConversionOperation
            return operation.Parent is IConversionOperation { Parent: IForEachLoopOperation };
        }

        private static bool IsOperationEnumeratedByInvocation(IOperation operation, ImmutableArray<IMethodSymbol> wellKnownEnumerationMethods)
        {
            if (operation.Parent is IArgumentOperation { Parent: IInvocationOperation invocationOperation } argumentOperation)
            {
                return IsInvocationCausingEnumerationOverArgument(invocationOperation, argumentOperation, wellKnownEnumerationMethods);
            }

            return false;
        }

        private static IInvocationOperation? GetLastDeferredExecutingInvocation(
            IArgumentOperation argumentOperation,
            IInvocationOperation invocationOperation,
            ImmutableArray<IMethodSymbol> wellKnownDeferredExecutingMethodsTakeOneIEnumerable,
            ImmutableArray<IMethodSymbol> wellKnownDeferredExecutionMethodsTakeTwoIEnumerables)
        {
            if (IsDeferredExecutingInvocation(invocationOperation, argumentOperation, wellKnownDeferredExecutingMethodsTakeOneIEnumerable, wellKnownDeferredExecutionMethodsTakeTwoIEnumerables))
            {
                // If the current invocation is deferred executing method, and we can walk up the invocation chain, check the parent.
                if (invocationOperation.Parent is IArgumentOperation { Parent: IInvocationOperation parentInvocationOperation } parentArgumentOperation
                    && IsDeferredExecutingInvocation(parentInvocationOperation, parentArgumentOperation, wellKnownDeferredExecutingMethodsTakeOneIEnumerable, wellKnownDeferredExecutionMethodsTakeTwoIEnumerables))
                {
                    return GetLastDeferredExecutingInvocation(parentArgumentOperation, parentInvocationOperation, wellKnownDeferredExecutingMethodsTakeOneIEnumerable, wellKnownDeferredExecutionMethodsTakeTwoIEnumerables);
                }

                // This is the last deferred executing invocation
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

            if (wellKnownMethodCausingEnumeration.Contains(targetMethod.OriginalDefinition)
                && targetMethod.IsExtensionMethod
                && !targetMethod.Parameters.IsEmpty
                && parameter.Equals(targetMethod.Parameters[0])
                && argumentOperationToCheck.Value.Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return true;
            }

            // If the method is accepting a parameter that constrained to IEnumerable<>
            // e.g.
            // void Bar<T>(T t) where T : IEnumerable<T> { }
            // Assuming it is going to enumerate the argument
            if (parameter.OriginalDefinition.Type is ITypeParameterSymbol typeParameterSymbol
                && typeParameterSymbol.ConstraintTypes.Any(type => type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T))
            {
                return true;
            }

            // TODO: we might want to have an attribute support here to mark a argument as 'enumerated'
            return false;
        }

        private static bool IsDeferredExecutingInvocation(
            IInvocationOperation invocationOperation,
            IArgumentOperation argumentOperationToCheck,
            ImmutableArray<IMethodSymbol> wellKnownDeferredExecutingMethodsTakeOneIEnumerable,
            ImmutableArray<IMethodSymbol> wellKnownDeferredExecutionMethodsTakeTwoIEnumerables)
        {
            RoslynDebug.Assert(invocationOperation.Arguments.Contains(argumentOperationToCheck));
            var targetMethod = invocationOperation.TargetMethod;
            if (argumentOperationToCheck.Value.Type.OriginalDefinition.SpecialType != SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return false;
            }

            var argumentMatchingParameter = argumentOperationToCheck.Parameter;
            // Method like Select, Where, etc.. only take one IEnumerable, and it is the first parameter.
            if (wellKnownDeferredExecutingMethodsTakeOneIEnumerable.Contains(targetMethod.OriginalDefinition)
                && !targetMethod.Parameters.IsEmpty
                && targetMethod.Parameters[0].Equals(argumentMatchingParameter))
            {
                return true;
            }

            // Method like Concat, Except, etc.. take two IEnumerable, and it is the first parameter or second parameter.
            if (wellKnownDeferredExecutionMethodsTakeTwoIEnumerables.Contains(targetMethod.OriginalDefinition)
                && targetMethod.Parameters.Length > 1
                && (targetMethod.Parameters[0].Equals(argumentMatchingParameter) || targetMethod.Parameters[1].Equals(argumentMatchingParameter)))
            {
                return true;
            }

            return false;
        }

        private static ImmutableArray<IMethodSymbol> GetWellKnownEnumerationMethods(WellKnownTypeProvider wellKnownTypeProvider)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            // Get linq method
            GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, s_wellKnownLinqMethodsCausingEnumeration, builder);

            // Get immutable collection conversion method, like ToImmutableArray()
            foreach (var (typeName, methodName) in s_wellKnownImmutableCollectionsHaveCovertMethod)
            {
                if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(typeName, out var type))
                {
                    var methods = type.GetMembers(methodName);
                    foreach (var method in methods)
                    {
                        // Usually there are two overloads for these methods, like ToImmutableArray,
                        // it has two overloads, one convert from ImmutableArray.Builder and one covert from IEnumerable<T>
                        // and we only want the last one
                        if (method is IMethodSymbol { Parameters: { Length: > 0 } parameters, IsExtensionMethod: true } methodSymbol
                            && parameters[0].Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                        {
                            builder.AddRange(methodSymbol);
                        }
                    }
                }
            }

            return builder.ToImmutable();
        }

        private static ImmutableArray<IMethodSymbol> GetWellKnownDeferredExecutionMethodsTakeOneIEnumerable(WellKnownTypeProvider wellKnownTypeProvider)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, s_wellKnownDeferredExecutionLinqMethodsTakeOneIEnumerable, builder);
            return builder.ToImmutable();
        }

        private static ImmutableArray<IMethodSymbol> GetWellKnownDeferredExecutionMethodTakesTwoIEnumerables(WellKnownTypeProvider wellKnownTypeProvider)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, s_wellKnownDeferredExecutionLinqMethodsTakeTwoIEnumerables, builder);
            return builder.ToImmutable();
        }

        private static void GetWellKnownMethods(
            WellKnownTypeProvider wellKnownTypeProvider,
            string typeName,
            ImmutableArray<string> methodNames,
            ArrayBuilder<IMethodSymbol> builder)
        {
            if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(typeName, out var type))
            {
                foreach (var methodName in methodNames)
                {
                    builder.AddRange(type.GetMembers(methodName).Cast<IMethodSymbol>());
                }
            }
        }
    }
}