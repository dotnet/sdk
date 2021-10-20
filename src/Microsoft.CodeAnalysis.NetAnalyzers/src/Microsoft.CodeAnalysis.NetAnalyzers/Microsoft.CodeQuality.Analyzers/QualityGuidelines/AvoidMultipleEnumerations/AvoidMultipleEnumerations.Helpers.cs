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
            ImmutableArray<IMethodSymbol> wellKnownDeferredExecutingMethods,
            ImmutableArray<IMethodSymbol> wellKnownEnumerationMethods)
        {
            RoslynDebug.Assert(operation is ILocalReferenceOperation or IParameterReferenceOperation);
            if (operation.Type.OriginalDefinition.SpecialType != SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return false;
            }

            var operationToCheck = SkipDeferredExecutingMethodIfNeeded(operation, wellKnownDeferredExecutingMethods);
            return IsOperationEnumeratedByInvocation(operationToCheck, wellKnownEnumerationMethods);
        }

        private static IOperation SkipDeferredExecutingMethodIfNeeded(
            IOperation operation,
            ImmutableArray<IMethodSymbol> wellKnownDeferredExecutingMethods)
        {
            if (operation.Parent is IArgumentOperation { Parent: IInvocationOperation invocationOperation } argumentOperation)
            {
                // Skip the linq chain in the middle if needed.
                // e.g.
                // c.Select(i => i + 1).Where(i != 10).First();
                // Go to the Where() invocation.
                var lastDeferredExecutingInvocation = GetLastDeferredExecutingInvocation(argumentOperation, invocationOperation, wellKnownDeferredExecutingMethods);
                return lastDeferredExecutingInvocation ?? operation;
            }

            return operation;
        }

        private static bool IsOperationEnumeratedByForEachLoop(
            IOperation operation,
            ImmutableArray<IMethodSymbol> wellKnownDeferredExecutingMethods)
        {
            RoslynDebug.Assert(operation is ILocalReferenceOperation or IParameterReferenceOperation);
            if (operation.Type.OriginalDefinition.SpecialType != SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return false;
            }

            var operationToCheck = SkipDeferredExecutingMethodIfNeeded(operation, wellKnownDeferredExecutingMethods);
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
            ImmutableArray<IMethodSymbol> wellKnownDeferredExecutingMethods)
        {
            if (IsDeferredExecutingInvocation(invocationOperation, argumentOperation, wellKnownDeferredExecutingMethods))
            {
                // If the current invocation is delay executing method, and we can walk up the invocation chain, check the parent.
                if (invocationOperation.Parent is IArgumentOperation { Parent: IInvocationOperation parentInvocationOperation } parentArgumentOperation
                    && IsDeferredExecutingInvocation(parentInvocationOperation, parentArgumentOperation, wellKnownDeferredExecutingMethods))
                {
                    return GetLastDeferredExecutingInvocation(parentArgumentOperation, parentInvocationOperation, wellKnownDeferredExecutingMethods);
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
            ImmutableArray<IMethodSymbol> wellKnownDeferredExecutingMethods)
        {
            RoslynDebug.Assert(invocationOperation.Arguments.Contains(argumentOperationToCheck));

            var targetMethod = invocationOperation.TargetMethod;
            // TODO: Consider hard code all the linq method cases here to make it more accurate
            return wellKnownDeferredExecutingMethods.Contains(targetMethod.OriginalDefinition)
                   && argumentOperationToCheck.Parameter.Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;
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

        private static ImmutableArray<IMethodSymbol> GetWellKnownDeferredExecutionMethod(WellKnownTypeProvider wellKnownTypeProvider)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, s_wellKnownDeferredExecutionLinqMethod, builder);
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