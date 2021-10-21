// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public partial class AvoidMultipleEnumerations
    {
        private static bool IsOperationEnumeratedByMethodInvocation(
            IOperation operation,
            ImmutableArray<IMethodSymbol> oneParameterDeferredMethods,
            ImmutableArray<IMethodSymbol> twoParametersDeferredMethods,
            ImmutableArray<IMethodSymbol> oneParameterEnumeratedMethods,
            ImmutableArray<IMethodSymbol> twoParametersEnumeratedMethods,
            ImmutableArray<ITypeSymbol> additionalDeferTypes)
        {
            RoslynDebug.Assert(operation is ILocalReferenceOperation or IParameterReferenceOperation);
            if (!IsDeferredType(operation.Type.OriginalDefinition, additionalDeferTypes))
            {
                return false;
            }

            var operationToCheck = SkipDeferredExecutingMethodIfNeeded(
                operation,
                oneParameterDeferredMethods,
                twoParametersDeferredMethods,
                additionalDeferTypes);
            return IsOperationEnumeratedByInvocation(operationToCheck, oneParameterEnumeratedMethods, twoParametersEnumeratedMethods, additionalDeferTypes);
        }

        private static IOperation SkipDeferredExecutingMethodIfNeeded(
            IOperation operation,
            ImmutableArray<IMethodSymbol> oneParameterDeferredMethods,
            ImmutableArray<IMethodSymbol> twoParametersDeferredMethods,
            ImmutableArray<ITypeSymbol> additionalDeferTypes)
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
                    oneParameterDeferredMethods,
                    twoParametersDeferredMethods,
                    additionalDeferTypes);
                return lastDeferredExecutingInvocation ?? operation;
            }

            return operation;
        }

        private static bool IsOperationEnumeratedByForEachLoop(
            IOperation operation,
            ImmutableArray<IMethodSymbol> oneParameterDeferredMethods,
            ImmutableArray<IMethodSymbol> twoParametersDeferredMethods,
            ImmutableArray<ITypeSymbol> additionalDeferTypes)
        {
            RoslynDebug.Assert(operation is ILocalReferenceOperation or IParameterReferenceOperation);
            if (!IsDeferredType(operation.Type.OriginalDefinition, additionalDeferTypes))
            {
                return false;
            }

            var operationToCheck = SkipDeferredExecutingMethodIfNeeded(operation, oneParameterDeferredMethods, twoParametersDeferredMethods, additionalDeferTypes);
            return IsTheExpressionOfForEachLoop(operationToCheck);
        }

        private static bool IsTheExpressionOfForEachLoop(IOperation operation)
        {
            // ForEach loop would convert all the expression to IEnumerable<T> before call the GetEnumerator method.
            // So the expression would be wrapped with a ConversionOperation
            return operation.Parent is IConversionOperation { Parent: IForEachLoopOperation };
        }

        private static bool IsOperationEnumeratedByInvocation(
            IOperation operation,
            ImmutableArray<IMethodSymbol> oneParameterEnumeratedMethods,
            ImmutableArray<IMethodSymbol> twoParametersEnumeratedMethods,
            ImmutableArray<ITypeSymbol> additionalDeferTypes)
        {
            if (operation.Parent is IArgumentOperation { Parent: IInvocationOperation invocationOperation } argumentOperation)
            {
                return IsInvocationCausingEnumerationOverArgument(
                    invocationOperation,
                    argumentOperation,
                    oneParameterEnumeratedMethods,
                    twoParametersEnumeratedMethods,
                    additionalDeferTypes);
            }

            return false;
        }

        private static IInvocationOperation? GetLastDeferredExecutingInvocation(
            IArgumentOperation argumentOperation,
            IInvocationOperation invocationOperation,
            ImmutableArray<IMethodSymbol> oneParameterDeferredMethods,
            ImmutableArray<IMethodSymbol> twoParametersDeferredMethods,
            ImmutableArray<ITypeSymbol> additionalDeferTypes)
        {
            if (IsDeferredExecutingInvocation(invocationOperation, argumentOperation, oneParameterDeferredMethods, twoParametersDeferredMethods, additionalDeferTypes))
            {
                // If the current invocation is deferred executing method, and we can walk up the invocation chain, check the parent.
                if (invocationOperation.Parent is IArgumentOperation { Parent: IInvocationOperation parentInvocationOperation } parentArgumentOperation
                    && IsDeferredExecutingInvocation(parentInvocationOperation, parentArgumentOperation, oneParameterDeferredMethods, twoParametersDeferredMethods, additionalDeferTypes))
                {
                    return GetLastDeferredExecutingInvocation(parentArgumentOperation, parentInvocationOperation, oneParameterDeferredMethods, twoParametersDeferredMethods, additionalDeferTypes);
                }

                // This is the last deferred executing invocation
                return invocationOperation;
            }

            return null;
        }

        private static bool IsInvocationCausingEnumerationOverArgument(
            IInvocationOperation invocationOperation,
            IArgumentOperation argumentOperationToCheck,
            ImmutableArray<IMethodSymbol> oneParameterEnumeratedMethods,
            ImmutableArray<IMethodSymbol> twoParametersEnumeratedMethods,
            ImmutableArray<ITypeSymbol> additionalDeferTypes)
        {
            RoslynDebug.Assert(invocationOperation.Arguments.Contains(argumentOperationToCheck));

            var targetMethod = invocationOperation.TargetMethod;
            var parameter = argumentOperationToCheck.Parameter;

            if (oneParameterEnumeratedMethods.Contains(targetMethod.OriginalDefinition)
                && targetMethod.IsExtensionMethod
                && !targetMethod.Parameters.IsEmpty
                && parameter.Equals(targetMethod.Parameters[0])
                && IsDeferredType(argumentOperationToCheck.Value.Type.OriginalDefinition, additionalDeferTypes))
            {
                return true;
            }

            // SequentialEqual would enumerate two parameters
            if (twoParametersEnumeratedMethods.Contains(targetMethod.OriginalDefinition)
                && targetMethod.IsExtensionMethod
                && targetMethod.Parameters.Length > 1
                && (parameter.Equals(targetMethod.Parameters[0]) || parameter.Equals(targetMethod.Parameters[1]))
                && IsDeferredType(argumentOperationToCheck.Value.Type.OriginalDefinition, additionalDeferTypes))
            {
                return true;
            }

            // If the method is accepting a parameter that constrained to IEnumerable<>
            // e.g.
            // void Bar<T>(T t) where T : IEnumerable<T> { }
            // Assuming it is going to enumerate the argument
            if (parameter.OriginalDefinition.Type is ITypeParameterSymbol typeParameterSymbol
                && typeParameterSymbol.ConstraintTypes.Any(type => IsDeferredType(type.OriginalDefinition, additionalDeferTypes)))
            {
                return true;
            }

            // TODO: we might want to have an attribute support here to mark a argument as 'enumerated'
            return false;
        }

        private static bool IsDeferredExecutingInvocation(
            IInvocationOperation invocationOperation,
            IArgumentOperation argumentOperationToCheck,
            ImmutableArray<IMethodSymbol> oneParameterDeferredMethods,
            ImmutableArray<IMethodSymbol> twoParameterDeferredMethods,
            ImmutableArray<ITypeSymbol> additionalDeferTypes)
        {
            RoslynDebug.Assert(invocationOperation.Arguments.Contains(argumentOperationToCheck));
            var targetMethod = invocationOperation.TargetMethod;
            if (!IsDeferredType(argumentOperationToCheck.Value.Type.OriginalDefinition, additionalDeferTypes))
            {
                return false;
            }

            var argumentMatchingParameter = argumentOperationToCheck.Parameter;
            // Method like Select, Where, etc.. only take one IEnumerable, and it is the first parameter.
            if (oneParameterDeferredMethods.Contains(targetMethod.OriginalDefinition)
                && !targetMethod.Parameters.IsEmpty
                && targetMethod.Parameters[0].Equals(argumentMatchingParameter))
            {
                return true;
            }

            // Method like Concat, Except, etc.. take two IEnumerable, and it is the first parameter or second parameter.
            if (twoParameterDeferredMethods.Contains(targetMethod.OriginalDefinition)
                && targetMethod.Parameters.Length > 1
                && (targetMethod.Parameters[0].Equals(argumentMatchingParameter) || targetMethod.Parameters[1].Equals(argumentMatchingParameter)))
            {
                return true;
            }

            return false;
        }

        private static ImmutableArray<IMethodSymbol> GetOneParameterEnumeratedMethods(WellKnownTypeProvider wellKnownTypeProvider)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            // Get linq method
            GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, s_linqOneParameterEnumeratedMethods, builder);

            // Get immutable collection conversion method, like ToImmutableArray()
            foreach (var (typeName, methodName) in s_immutableCollectionsTypeNamesAndConvensionMethods)
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

        private static bool IsDeferredType(ITypeSymbol type, ImmutableArray<ITypeSymbol> additionalSymbolsToCheck)
            => type.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T || additionalSymbolsToCheck.Contains(type);

        private static ImmutableArray<ITypeSymbol> GetTypes(Compilation compilation, ImmutableArray<string> typeNames)
        {
            using var builder = ArrayBuilder<ITypeSymbol>.GetInstance();
            foreach (var name in typeNames)
            {
                if (compilation.TryGetOrCreateTypeByMetadataName(name, out var typeSymbol))
                {
                    builder.Add(typeSymbol);
                }
            }

            return builder.ToImmutable();
        }

        private static ImmutableArray<IMethodSymbol> GetTwoParametersEnumeratedMethods(WellKnownTypeProvider wellKnownTypeProvider)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, s_linqTwoParametersEnumeratedMethods, builder);
            return builder.ToImmutable();
        }

        private static ImmutableArray<IMethodSymbol> GetOneParameterDeferredMethods(WellKnownTypeProvider wellKnownTypeProvider)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, s_linqOneParameterDeferredMethods, builder);
            return builder.ToImmutable();
        }

        private static ImmutableArray<IMethodSymbol> GetTwoParametersDeferredMethods(WellKnownTypeProvider wellKnownTypeProvider)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, s_linqTwoParametersDeferredMethods, builder);
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