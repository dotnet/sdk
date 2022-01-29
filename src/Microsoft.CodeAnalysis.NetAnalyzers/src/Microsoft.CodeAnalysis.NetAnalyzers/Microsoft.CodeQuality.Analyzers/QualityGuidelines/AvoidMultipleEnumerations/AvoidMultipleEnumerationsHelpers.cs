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
    internal static class AvoidMultipleEnumerationsHelpers
    {
        /// <summary>
        /// Check if the LocalReferenceOperation or ParameterReferenceOperation is enumerated by a method invocation.
        /// </summary>
        public static bool IsOperationEnumeratedByMethodInvocation(
            IOperation operation,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            RoslynDebug.Assert(operation is ILocalReferenceOperation or IParameterReferenceOperation);
            if (!IsDeferredType(operation.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            var operationToCheck = SkipLinqChainAndConversionMethodIfNeeded(
                operation,
                wellKnownSymbolsInfo);
            return IsOperationEnumeratedByInvocation(operationToCheck, wellKnownSymbolsInfo);
        }

        /// <summary>
        /// Skip the deferred method call and conversion operation in linq methods call chain
        /// </summary>
        public static IOperation SkipLinqChainAndConversionMethodIfNeeded(
            IOperation operation,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            if (IsValidImplicitConversion(operation.Parent, wellKnownSymbolsInfo))
            {
                // Go to the implicit conversion if needed
                // e.g.
                // void Bar (IOrderedEnumerable<T> c)
                // {
                //      c.First();
                // }
                // here 'c' would be converted to IEnumerable<T>
                return SkipLinqChainAndConversionMethodIfNeeded(operation.Parent, wellKnownSymbolsInfo);
            }

            if (IsOperationTheArgumentOfLinqChainInvocation(operation.Parent, wellKnownSymbolsInfo))
            {
                // This operation is used as an argument of a deferred execution method.
                // Check if the invocation of the deferred execution method is used in another deferred execution method.
                return SkipLinqChainAndConversionMethodIfNeeded(operation.Parent.Parent, wellKnownSymbolsInfo);
            }

            if (IsInstanceOfLinqChainInvocation(operation, wellKnownSymbolsInfo))
            {
                // If the extension method could be used as reduced method, also check the its invocation instance.
                // Like in VB,
                // 'i.Select(Function(a) a)', 'i' is the invocation instance of 'Select'
                return SkipLinqChainAndConversionMethodIfNeeded(operation.Parent, wellKnownSymbolsInfo);
            }

            return operation;
        }

        public static bool IsValidImplicitConversion(IOperation operation, WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            // Check if this is an implicit conversion operation convert from one delay type to another delay type.
            // This is used in methods chain like
            // 1. Cast<T> and OfType<T>, which takes IEnumerable as the first parameter. For example:
            //    c.Select(i => i + 1).Cast<long>();
            //    'c.Select(i => i + 1)' has IEnumerable<T> type, and will be implicitly converted to IEnumerable. Then the conversion result would be passed to Cast<long>().
            // 2. OrderBy, ThenBy, etc.. which returns IOrderedIEnumerable<T>. For this example,
            //    c.OrderBy(i => i.Key).Select(m => m + 1);
            //    'c.OrderBy(i => i.Key)' has IOrderedIEnumerable<T> type, and will be implicitly converted to IEnumerable<T> . Then the conversion result would be passed to Select()
            // 3. For each loop in C#. C# binder would create a conversion for the collection before calling GetEnumerator()
            //    Note: this is not true for VB, VB binder won't generate the conversion.
            return operation is IConversionOperation { IsImplicit: true } conversionOperation
                   && IsDeferredType(conversionOperation.Operand.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes)
                   && IsDeferredType(conversionOperation.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes);
        }

        /// <summary>
        /// Check if the LocalReferenceOperation or ParameterReferenceOperation is enumerated by for each loop
        /// </summary>
        public static bool IsOperationEnumeratedByForEachLoop(
            IOperation operation,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            RoslynDebug.Assert(operation is ILocalReferenceOperation or IParameterReferenceOperation);
            if (!IsDeferredType(operation.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            var operationToCheck = SkipLinqChainAndConversionMethodIfNeeded(operation, wellKnownSymbolsInfo);
            return operationToCheck.Parent is IForEachLoopOperation forEachLoopOperation && forEachLoopOperation.Collection == operationToCheck;
        }

        private static bool IsInstanceOfLinqChainInvocation(IOperation operation, WellKnownSymbolsInfo wellKnownSymbolsInfo)
            => operation.Parent is IInvocationOperation invocationOperation
               && invocationOperation.Instance == operation
               && IsLinqChainInvocation(invocationOperation, wellKnownSymbolsInfo);

        private static bool IsOperationEnumeratedByInvocation(
            IOperation operation,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            // Case 1:
            // For C# or the method is called as an ordinary method,
            // 'i.ElementAt(10)', is essentially 'ElementAt(i, 10)'
            if (operation.Parent is IArgumentOperation { Parent: IInvocationOperation grandParentInvocationOperation } parentArgumentOperation)
            {
                return IsInvocationCausingEnumerationOverArgument(
                    grandParentInvocationOperation,
                    parentArgumentOperation,
                    wellKnownSymbolsInfo);
            }

            // Case 2:
            // If the method is in reduced form.
            // Like in VB,
            // 'i.ElementAt(10)', 'i' is thought as the invocation instance.
            if (operation.Parent is IInvocationOperation { TargetMethod.MethodKind: MethodKind.ReducedExtension } parentInvocationOperation
                && operation == parentInvocationOperation.Instance)
            {
                return IsInvocationCausingEnumerationOverInvocationInstance(parentInvocationOperation, wellKnownSymbolsInfo);
            }

            return false;
        }

        /// <summary>
        /// Get the original parameter symbol in the ReducedFromMethod.
        /// </summary>
        private static IParameterSymbol GetReducedFromParameter(IMethodSymbol methodSymbol, IParameterSymbol parameterSymbol)
        {
            RoslynDebug.Assert(methodSymbol.Parameters.Contains(parameterSymbol));
            RoslynDebug.Assert(methodSymbol.ReducedFrom != null);

            var reducedFromMethodSymbol = methodSymbol.ReducedFrom;
            var index = methodSymbol.Parameters.IndexOf(parameterSymbol);
            return reducedFromMethodSymbol.Parameters[index + 1];
        }

        /// <summary>
        /// Return true if the target method of the <param name="invocationOperation"/> is a reduced extension method, and it will enumerate its invocation instance.
        /// </summary>
        private static bool IsInvocationCausingEnumerationOverInvocationInstance(IInvocationOperation invocationOperation, WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            if (invocationOperation.Instance == null || invocationOperation.TargetMethod.MethodKind != MethodKind.ReducedExtension)
            {
                return false;
            }

            if (!IsDeferredType(invocationOperation.Instance.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            var originalTargetMethod = invocationOperation.TargetMethod.ReducedFrom.OriginalDefinition;
            if (originalTargetMethod.Parameters.IsEmpty)
            {
                return false;
            }

            if (wellKnownSymbolsInfo.NoEnumerationMethods.Contains(originalTargetMethod))
            {
                return false;
            }

            // Well known linq methods, like 'ElementAt', or user specified methods
            if (wellKnownSymbolsInfo.EnumeratedMethods.Contains(originalTargetMethod)
                || (wellKnownSymbolsInfo.CustomizedNoEnumerationMethods != null && wellKnownSymbolsInfo.CustomizedNoEnumerationMethods.Contains(originalTargetMethod)))
            {
                return true;
            }

            var invocationInstanceParameter = originalTargetMethod.Parameters[0];
            return IsDeferredType(invocationInstanceParameter.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes)
                   || (invocationInstanceParameter.OriginalDefinition.Type is ITypeParameterSymbol typeParameterSymbol && IsConstraintTypesHasDeferredType(typeParameterSymbol, wellKnownSymbolsInfo.AdditionalDeferredTypes));
        }

        /// <summary>
        /// Check if <param name="invocationOperation"/> is targeting a method that will cause the enumeration of <param name="argumentOperationToCheck"/>.
        /// </summary>
        private static bool IsInvocationCausingEnumerationOverArgument(
            IInvocationOperation invocationOperation,
            IArgumentOperation argumentOperationToCheck,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            RoslynDebug.Assert(invocationOperation.Arguments.Contains(argumentOperationToCheck));
            if (!IsDeferredType(argumentOperationToCheck.Value.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            var targetMethod = invocationOperation.TargetMethod;
            var reducedFromMethod = targetMethod.ReducedFrom ?? targetMethod;
            var argumentMappingParameter = targetMethod.MethodKind == MethodKind.ReducedExtension
                ? GetReducedFromParameter(targetMethod, argumentOperationToCheck.Parameter)
                : argumentOperationToCheck.Parameter;

            if (wellKnownSymbolsInfo.NoEnumerationMethods.Contains(reducedFromMethod.OriginalDefinition))
            {
                return false;
            }

            // User specified methods
            if (wellKnownSymbolsInfo.CustomizedNoEnumerationMethods != null && wellKnownSymbolsInfo.CustomizedNoEnumerationMethods.Contains(reducedFromMethod))
            {
                return false;
            }

            // Common linq method case, like ElementAt
            if (wellKnownSymbolsInfo.EnumeratedMethods.Contains(reducedFromMethod.OriginalDefinition)
                && reducedFromMethod.Parameters.Any(
                    methodParameter => IsDeferredType(methodParameter.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes) && methodParameter.Equals(argumentMappingParameter)))
            {
                return true;
            }

            // The type of mapping parameter is IEnumerable, by default think it is going to enumerate the argument.
            if (IsDeferredType(argumentMappingParameter.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return true;
            }

            // If the method is accepting a parameter that constrained to IEnumerable<>
            // e.g.
            // void Bar<T>(T t) where T : IEnumerable<T> { }
            // Assuming it is going to enumerate the argument
            return argumentMappingParameter.OriginalDefinition.Type is ITypeParameterSymbol typeParameterSymbol
                   && IsConstraintTypesHasDeferredType(typeParameterSymbol, wellKnownSymbolsInfo.AdditionalDeferredTypes);
        }

        /// <summary>
        /// Check if <param name="operation"/> is an argument that passed into a linq chain. (like Select, Where etc.)
        /// </summary>
        private static bool IsOperationTheArgumentOfLinqChainInvocation(IOperation operation, WellKnownSymbolsInfo wellKnownSymbolsInfo)
            => operation is IArgumentOperation { Parent: IInvocationOperation invocationOperation } argumentOperation
                && IsLinqChainInvocation(invocationOperation, argumentOperation, wellKnownSymbolsInfo);

        /// <summary>
        /// Check if <param name="argumentOperationToCheck"/> is passed as a deferred executing argument into <param name="invocationOperation"/>.
        /// </summary>
        public static bool IsLinqChainInvocation(
            IInvocationOperation invocationOperation,
            IArgumentOperation argumentOperationToCheck,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            RoslynDebug.Assert(invocationOperation.Arguments.Contains(argumentOperationToCheck));
            if (!IsDeferredType(argumentOperationToCheck.Value.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            var targetMethod = invocationOperation.TargetMethod;
            // For C#, extension method is used as an ordinary static method.
            // For VB, ex: a.Concat(b)
            // 'b' is an argument to 'Concat', which is a reduced method.
            var reducedFromMethod = targetMethod.ReducedFrom ?? targetMethod;
            var returnType = reducedFromMethod.ReturnType.OriginalDefinition;
            if (!IsDeferredType(returnType, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            if (returnType is ITypeParameterSymbol typeParameterSymbol &&
                !IsConstraintTypesHasDeferredType(typeParameterSymbol, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            // For VB, ex: a.Concat(b)
            // 'b' is the first argument to 'Concat', because it is a reduced method. But 'b' is the second parameter of 'Concat'
            var argumentMappingParameter = targetMethod.MethodKind == MethodKind.ReducedExtension
                ? GetReducedFromParameter(targetMethod, argumentOperationToCheck.Parameter)
                : argumentOperationToCheck.Parameter;

            if (wellKnownSymbolsInfo.LinqChainMethods.Contains(reducedFromMethod.OriginalDefinition)
                && reducedFromMethod.Parameters.Any(
                    methodParameter => IsDeferredType(methodParameter.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes) && methodParameter.Equals(argumentMappingParameter)))
            {
                return true;
            }

            return wellKnownSymbolsInfo.CustomizedLinqChainMethods != null &&
                   wellKnownSymbolsInfo.CustomizedLinqChainMethods.Contains(reducedFromMethod.OriginalDefinition);
        }

        /// <summary>
        /// Return true if the TargetMethod of <param name="invocationOperation"/> is a reduced extension method, and is a Linq chain methods
        /// </summary>
        public static bool IsLinqChainInvocation(IInvocationOperation invocationOperation, WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            if (invocationOperation.Instance == null
                || invocationOperation.TargetMethod.MethodKind != MethodKind.ReducedExtension
                || !IsDeferredType(invocationOperation.Instance.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            var originalTargetMethod = invocationOperation.TargetMethod.ReducedFrom.OriginalDefinition;
            var returnType = invocationOperation.TargetMethod.ReturnType;
            if (originalTargetMethod.Parameters.IsEmpty
                || !IsDeferredType(returnType, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            if (returnType is ITypeParameterSymbol typeParameterSymbol &&
                !IsConstraintTypesHasDeferredType(typeParameterSymbol, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            if (!IsDeferredType(originalTargetMethod.Parameters[0].Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            return wellKnownSymbolsInfo.LinqChainMethods.Contains(originalTargetMethod);
        }

        public static ImmutableArray<IMethodSymbol> GetEnumeratedMethods(WellKnownTypeProvider wellKnownTypeProvider,
            ImmutableArray<(string typeName, string methodName)> typeAndMethodNames,
            ImmutableArray<string> linqMethodNames)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            GetImmutableCollectionConversionMethods(wellKnownTypeProvider, typeAndMethodNames, builder);
            GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, linqMethodNames, builder);
            return builder.ToImmutable();
        }

        private static void GetImmutableCollectionConversionMethods(
            WellKnownTypeProvider wellKnownTypeProvider,
            ImmutableArray<(string, string)> typeAndMethodNames,
            ArrayBuilder<IMethodSymbol> builder)
        {
            // Get immutable collection conversion method, like ToImmutableArray()
            foreach (var (typeName, methodName) in typeAndMethodNames)
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
        }

        public static ImmutableArray<IMethodSymbol> GetGetEnumeratorMethods(WellKnownTypeProvider wellKnownTypeProvider)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();

            if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsIEnumerable, out var nonGenericIEnumerable))
            {
                var method = nonGenericIEnumerable.GetMembers(WellKnownMemberNames.GetEnumeratorMethodName).FirstOrDefault();
                if (method is IMethodSymbol methodSymbol)
                {
                    builder.Add(methodSymbol);
                }
            }

            if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIEnumerable1, out var genericIEnumerable))
            {
                var method = genericIEnumerable.GetMembers(WellKnownMemberNames.GetEnumeratorMethodName).FirstOrDefault();
                if (method is IMethodSymbol methodSymbol)
                {
                    builder.Add(methodSymbol);
                }
            }

            return builder.ToImmutable();
        }

        private static bool IsConstraintTypesHasDeferredType(ITypeParameterSymbol typeParameterSymbol, ImmutableArray<ITypeSymbol> additionalTypesToCheck)
            => typeParameterSymbol.ConstraintTypes.Any(type => IsDeferredType(type?.OriginalDefinition, additionalTypesToCheck));

        public static bool IsDeferredType(ITypeSymbol? type, ImmutableArray<ITypeSymbol> additionalTypesToCheck)
        {
            if (type == null)
            {
                return false;
            }

            return type.SpecialType is SpecialType.System_Collections_Generic_IEnumerable_T or SpecialType.System_Collections_IEnumerable
                || additionalTypesToCheck.Contains(type);
        }

        public static ImmutableArray<ITypeSymbol> GetTypes(Compilation compilation, ImmutableArray<string> typeNames)
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

        public static ImmutableArray<IMethodSymbol> GetLinqMethods(WellKnownTypeProvider wellKnownTypeProvider, ImmutableArray<string> methodNames)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, methodNames, builder);
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
                    builder.AddRange(type.GetMembers(methodName).OfType<IMethodSymbol>());
                }
            }
        }
    }
}