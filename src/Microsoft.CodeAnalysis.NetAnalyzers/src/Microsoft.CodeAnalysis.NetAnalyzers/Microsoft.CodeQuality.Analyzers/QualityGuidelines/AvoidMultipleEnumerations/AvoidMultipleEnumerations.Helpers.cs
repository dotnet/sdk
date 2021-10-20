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
            ImmutableArray<IMethodSymbol> wellKnownDelayExecutingMethods,
            ImmutableArray<IMethodSymbol> wellKnownEnumerationMethods)
        {
            RoslynDebug.Assert(operation is ILocalReferenceOperation or IParameterReferenceOperation);
            if (operation.Type.OriginalDefinition.SpecialType != SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return false;
            }

            var operationToCheck = SkipDelayExecutingMethodIfNeeded(operation, wellKnownDelayExecutingMethods);
            return IsOperationEnumeratedByInvocation(operationToCheck, wellKnownEnumerationMethods);
        }

        private static IOperation SkipDelayExecutingMethodIfNeeded(
            IOperation operation,
            ImmutableArray<IMethodSymbol> wellKnownDelayExecutingMethods)
        {
            if (operation.Parent is IArgumentOperation { Parent: IInvocationOperation invocationOperation } argumentOperation)
            {
                // Skip the linq chain in the middle if needed.
                // e.g.
                // c.Select(i => i + 1).Where(i != 10).First();
                // Go to the Where() invocation.
                var lastDelayExecutingInvocation = GetLastDelayExecutingInvocation(argumentOperation, invocationOperation, wellKnownDelayExecutingMethods);
                return lastDelayExecutingInvocation ?? operation;
            }

            return operation;
        }

        private static bool IsOperationEnumeratedByForEachLoop(
            IOperation operation,
            ImmutableArray<IMethodSymbol> wellKnownDelayExecutingMethods)
        {
            RoslynDebug.Assert(operation is ILocalReferenceOperation or IParameterReferenceOperation);
            if (operation.Type.OriginalDefinition.SpecialType != SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return false;
            }

            var operationToCheck = SkipDelayExecutingMethodIfNeeded(operation, wellKnownDelayExecutingMethods);
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

        private static IInvocationOperation? GetLastDelayExecutingInvocation(
            IArgumentOperation argumentOperation,
            IInvocationOperation invocationOperation,
            ImmutableArray<IMethodSymbol> wellKnownDelayExecutingMethods)
        {
            if (IsDelayExecutingInvocation(invocationOperation, argumentOperation, wellKnownDelayExecutingMethods))
            {
                // If the current invocation is delay executing method, and we can walk up the invocation chain, check the parent.
                if (invocationOperation.Parent is IArgumentOperation { Parent: IInvocationOperation parentInvocationOperation } parentArgumentOperation
                    && IsDelayExecutingInvocation(parentInvocationOperation, parentArgumentOperation, wellKnownDelayExecutingMethods))
                {
                    return GetLastDelayExecutingInvocation(parentArgumentOperation, parentInvocationOperation, wellKnownDelayExecutingMethods);
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

            // Don't rely one the sequence of the argument to prevent this case
            // var x = Enumerable.First(predicate: x => x != 10, bar);
            if (wellKnownMethodCausingEnumeration.Contains(targetMethod.OriginalDefinition)
                && parameter.Name == parameterNameInLinqMethod
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

        private static bool IsDelayExecutingInvocation(
            IInvocationOperation invocationOperation,
            IArgumentOperation argumentOperationToCheck,
            ImmutableArray<IMethodSymbol> wellKnownDelayExecutingMethods)
        {
            RoslynDebug.Assert(invocationOperation.Arguments.Contains(argumentOperationToCheck));

            var targetMethod = invocationOperation.TargetMethod;
            // TODO: Consider hard code all the linq method cases here to make it more accurate
            return wellKnownDelayExecutingMethods.Contains(targetMethod.OriginalDefinition)
                   && argumentOperationToCheck.Parameter.Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;
        }

        private static ImmutableArray<IMethodSymbol> GetWellKnownEnumerationMethods(WellKnownTypeProvider wellKnownTypeProvider)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, s_wellKnownLinqMethodsCausingEnumeration, builder);

            foreach (var (typeName, methodName) in s_wellKnownImmutableCollectionsHaveCovertMethod)
            {
                if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(typeName, out var type))
                {
                    builder.AddRange(type.GetMembers(methodName).Cast<IMethodSymbol>());
                }
            }

            return builder.ToImmutable();
        }

        private static ImmutableArray<IMethodSymbol> GetWellKnownDelayExecutionMethod(WellKnownTypeProvider wellKnownTypeProvider)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, s_wellKnownDelayExecutionLinqMethod, builder);
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