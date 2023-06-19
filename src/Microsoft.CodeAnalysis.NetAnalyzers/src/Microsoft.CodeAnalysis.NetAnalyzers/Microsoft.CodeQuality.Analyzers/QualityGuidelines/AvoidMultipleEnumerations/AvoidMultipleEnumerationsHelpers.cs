// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    internal static class AvoidMultipleEnumerationsHelpers
    {
        /// <summary>
        /// Skip the deferred method call and conversion operation in Linq methods call chain.
        /// Return the tail of the Linq chain operation, and possible enumerationCount by the Linq Chain Method.
        /// </summary>
        public static (IOperation linqChainTailOperation, EnumerationCount linqChainEnumerationCount) SkipLinqChainAndConversionMethod(
            IOperation operation,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            RoslynDebug.Assert(operation is IParameterReferenceOperation or ILocalReferenceOperation);
            return VisitLinqChainAndCoversionMethod(operation, EnumerationCount.Zero, wellKnownSymbolsInfo);

            static (IOperation linqChainTailOperation, EnumerationCount enumerationCount) VisitLinqChainAndCoversionMethod(
                IOperation operation,
                EnumerationCount enumerationCount,
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
                    return VisitLinqChainAndCoversionMethod(operation.Parent, enumerationCount, wellKnownSymbolsInfo);
                }

                if (IsOperationIsArgumentOfLinqChainInvocation(operation.Parent, wellKnownSymbolsInfo, out var enumerateArgument))
                {
                    // This operation is used as an argument of a deferred execution method.
                    // Check if the invocation of the deferred execution method is used in another deferred execution method.
                    return VisitLinqChainAndCoversionMethod(
                        operation.Parent.Parent!,
                        enumerateArgument
                            ? InvocationSetHelpers.AddInvocationCount(enumerationCount, EnumerationCount.One)
                            : enumerationCount,
                        wellKnownSymbolsInfo);
                }

                if (IsInstanceOfLinqChainInvocation(operation, wellKnownSymbolsInfo, out var enumerateInstance))
                {
                    // If the extension method could be used as reduced method, also check its invocation instance.
                    // Like in VB,
                    // 'i.Select(Function(a) a)', 'i' is the invocation instance of 'Select'
                    return VisitLinqChainAndCoversionMethod(
                        operation.Parent!,
                        enumerateInstance
                            ? InvocationSetHelpers.AddInvocationCount(enumerationCount, EnumerationCount.One)
                            : enumerationCount,
                        wellKnownSymbolsInfo);
                }

                return (operation, enumerationCount);
            }
        }

        public static bool IsValidImplicitConversion([NotNullWhen(true)] IOperation? operation, WellKnownSymbolsInfo wellKnownSymbolsInfo)
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
        /// Check if the operation is deferred type and also it is a collecton enumerated by a for each loop.
        /// </summary>
        public static bool IsOperationEnumeratedByForEachLoop(
            IOperation operation,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            if (!IsDeferredType(operation.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            return operation.Parent is IForEachLoopOperation forEachLoopOperation && forEachLoopOperation.Collection == operation;
        }

        private static bool IsInstanceOfLinqChainInvocation(
            IOperation operation, WellKnownSymbolsInfo wellKnownSymbolsInfo, out bool enumerateInstance)
        {
            if (operation.Parent is IInvocationOperation invocationOperation
               && invocationOperation.Instance == operation
               && IsLinqChainInvocation(invocationOperation, wellKnownSymbolsInfo, out enumerateInstance))
            {
                return true;
            }

            enumerateInstance = false;
            return false;
        }

        public static bool IsOperationEnumeratedByInvocation(
            IOperation operation,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            // Case 1:
            // For C# or the method is called as an ordinary method,
            // 'i.ElementAt(10)', is essentially 'ElementAt(i, 10)'
            if (operation.Parent is IArgumentOperation parentArgumentOperation)
            {
                if (parentArgumentOperation.Parent is IInvocationOperation grandParentInvocationOperation)
                {
                    return IsInvocationCausingEnumerationOverArgument(
                        grandParentInvocationOperation,
                        parentArgumentOperation,
                        wellKnownSymbolsInfo);
                }
                else if (parentArgumentOperation.Parent is IObjectCreationOperation grandParentObjectCreationOperation)
                {
                    return IsObjectCreationOperationCausingEnumerationOverArgument(
                       grandParentObjectCreationOperation,
                       parentArgumentOperation,
                       wellKnownSymbolsInfo);
                }
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

            var reducedFromMethodSymbol = methodSymbol.ReducedFrom.OriginalDefinition;
            var index = methodSymbol.Parameters.IndexOf(parameterSymbol);
            return reducedFromMethodSymbol.Parameters[index + 1];
        }

        /// <summary>
        /// Return true if the target method of the <param name="invocationOperation"/> is a reduced extension method, and it will enumerate its invocation instance.
        /// </summary>
        private static bool IsInvocationCausingEnumerationOverInvocationInstance(IInvocationOperation invocationOperation, WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            if (invocationOperation.Instance == null
                || invocationOperation.TargetMethod.MethodKind != MethodKind.ReducedExtension
                || !IsDeferredType(invocationOperation.Instance.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            var originalTargetMethod = invocationOperation.TargetMethod.ReducedFrom!.OriginalDefinition;
            // Well-known linq methods, like 'TryGetNonEnumeratedCount'
            if (originalTargetMethod.Parameters.IsEmpty || wellKnownSymbolsInfo.NoEnumerationMethods.Contains(originalTargetMethod))
            {
                return false;
            }

            // Well-known linq methods, like 'ElementAt'
            if (wellKnownSymbolsInfo.EnumeratedMethods.Contains(originalTargetMethod))
            {
                return true;
            }

            // User defined method from editor config
            return wellKnownSymbolsInfo.IsCustomizedLinqChainMethods(originalTargetMethod);
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
            RoslynDebug.Assert(!IsLinqChainInvocation(invocationOperation, argumentOperationToCheck, wellKnownSymbolsInfo, out _));
            return IsInvokingMethodEnumeratedOverArgument(invocationOperation.TargetMethod, argumentOperationToCheck, wellKnownSymbolsInfo);
        }

        private static bool IsObjectCreationOperationCausingEnumerationOverArgument(
            IObjectCreationOperation objectCreationOperation,
            IArgumentOperation argumentOperation,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            RoslynDebug.Assert(objectCreationOperation.Arguments.Contains(argumentOperation));
            return IsInvokingMethodEnumeratedOverArgument(objectCreationOperation.Constructor, argumentOperation, wellKnownSymbolsInfo);
        }

        private static bool IsInvokingMethodEnumeratedOverArgument(
            IMethodSymbol? invokingMethod,
            IArgumentOperation argumentOperation,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            if (invokingMethod == null ||
                argumentOperation.Parameter == null ||
                !IsDeferredType(argumentOperation.Value.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            var reducedFromMethod = invokingMethod.ReducedFrom ?? invokingMethod;
            var originalMethod = reducedFromMethod.OriginalDefinition;

            if (wellKnownSymbolsInfo.NoEnumerationMethods.Contains(originalMethod))
            {
                return false;
            }

            var argumentMappingParameter = invokingMethod.MethodKind == MethodKind.ReducedExtension
                ? GetReducedFromParameter(invokingMethod, argumentOperation.Parameter)
                : argumentOperation.Parameter.OriginalDefinition;

            // Common linq method case, like ElementAt
            if (wellKnownSymbolsInfo.EnumeratedMethods.Contains(originalMethod)
                && originalMethod.Parameters.Any(
                    methodParameter => IsDeferredType(methodParameter.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes) && methodParameter.Equals(argumentMappingParameter)))
            {
                return true;
            }

            // Enumeration methods specified in editorConfig
            if (wellKnownSymbolsInfo.IsCustomizedEnumerationMethods(originalMethod))
            {
                return true;
            }

            // Analyzer is in aggressive mode, assuming all methods enumerated the argument if we know the type of mapping parameter is IEnumerable type.
            return wellKnownSymbolsInfo.AssumeMethodEnumeratesParameters
                && IsDeferredType(argumentMappingParameter.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes);
        }

        /// <summary>
        /// Check if <param name="operation"/> is an argument that passed into a linq chain. (like Select, Where etc.)
        /// </summary>
        private static bool IsOperationIsArgumentOfLinqChainInvocation(
            [NotNullWhen(true)] IOperation? operation, WellKnownSymbolsInfo wellKnownSymbolsInfo, out bool enumerateArgument)
        {
            if (operation is IArgumentOperation { Parent: IInvocationOperation invocationOperation } argumentOperation)
            {
                return IsLinqChainInvocation(invocationOperation, argumentOperation, wellKnownSymbolsInfo, out enumerateArgument);
            }

            enumerateArgument = false;
            return false;
        }

        /// <summary>
        /// Check if <param name="argumentOperationToCheck"/> is passed as a deferred executing argument into <param name="invocationOperation"/>.
        /// </summary>
        public static bool IsLinqChainInvocation(
            IInvocationOperation invocationOperation,
            IArgumentOperation argumentOperationToCheck,
            WellKnownSymbolsInfo wellKnownSymbolsInfo,
            out bool enumerateArgument)
        {
            enumerateArgument = false;
            RoslynDebug.Assert(invocationOperation.Arguments.Contains(argumentOperationToCheck));
            if (argumentOperationToCheck.Parameter == null ||
                !IsDeferredType(argumentOperationToCheck.Value.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            var targetMethod = invocationOperation.TargetMethod;
            // For C#, extension method is used as an ordinary static method.
            // For VB, ex: a.Concat(b)
            // 'b' is an argument to 'Concat', which is a reduced method.
            var reducedFromMethod = targetMethod.ReducedFrom ?? targetMethod;
            var originalMethod = reducedFromMethod.OriginalDefinition;
            if (!IsDeferredType(targetMethod.ReturnType.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            // For VB, ex: a.Concat(b)
            // 'b' is in fact the first argument to 'Concat', because the extension method in VB is reduced.
            var argumentMappingParameter = targetMethod.MethodKind == MethodKind.ReducedExtension
                ? GetReducedFromParameter(targetMethod, argumentOperationToCheck.Parameter)
                : argumentOperationToCheck.Parameter;

            if (wellKnownSymbolsInfo.LinqChainMethods.Contains(originalMethod)
                && originalMethod.Parameters.Any(
                    methodParameter => IsDeferredType(methodParameter.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes) && methodParameter.Equals(argumentMappingParameter.OriginalDefinition)))
            {
                // All well-known linq chain methods under Linq namespace won't enumerate the argument.
                // e.g. For methods like 'Select', 'Where', etc..
                // call 'a.Select(i => i + 1)' won't enumerate 'a'
                return true;
            }

            if (wellKnownSymbolsInfo.IsCustomizedLinqChainMethods(originalMethod))
            {
                enumerateArgument = wellKnownSymbolsInfo.IsCustomizedEnumerationMethods(originalMethod);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Return true if the TargetMethod of <param name="invocationOperation"/> is a reduced extension method, and is a Linq chain methods
        /// </summary>
        public static bool IsLinqChainInvocation(IInvocationOperation invocationOperation, WellKnownSymbolsInfo wellKnownSymbolsInfo, out bool enumerateInstance)
        {
            enumerateInstance = false;
            if (invocationOperation.Instance == null
                || invocationOperation.TargetMethod.MethodKind != MethodKind.ReducedExtension
                || !IsDeferredType(invocationOperation.Instance.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes)
                || !IsDeferredType(invocationOperation.TargetMethod.ReturnType.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            var originalMethod = invocationOperation.TargetMethod.ReducedFrom!.OriginalDefinition;
            if (wellKnownSymbolsInfo.LinqChainMethods.Contains(originalMethod))
            {
                return true;
            }

            if (wellKnownSymbolsInfo.IsCustomizedLinqChainMethods(originalMethod))
            {
                enumerateInstance = wellKnownSymbolsInfo.IsCustomizedEnumerationMethods(originalMethod);
                return true;
            }

            return false;
        }

        public static ImmutableArray<IMethodSymbol> GetEnumeratedMethods(WellKnownTypeProvider wellKnownTypeProvider,
            ImmutableArray<(string typeName, string methodName)> typeAndMethodNames,
            ImmutableArray<string> linqMethodNames,
            ImmutableArray<string> constructorTypeNames)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            GetImmutableCollectionConversionMethods(wellKnownTypeProvider, typeAndMethodNames, builder);
            GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, linqMethodNames, builder);
            GetConstructors(wellKnownTypeProvider, constructorTypeNames, builder);
            return builder.ToImmutable();
        }

        public static void GetConstructors(
            WellKnownTypeProvider wellKnownTypeProvider,
            ImmutableArray<string> typeNames,
            ArrayBuilder<IMethodSymbol> builder)
        {
            foreach (var typeName in typeNames)
            {
                if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(typeName, out var type))
                {
                    builder.AddRange(type.Constructors.Where(c => c.Parameters.Any(p => p.Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)));
                }
            }
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
                            builder.Add(methodSymbol);
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
            => type switch
            {
                null => false,
                ITypeParameterSymbol typeParameterSymbol => IsConstraintTypesHasDeferredType(typeParameterSymbol, additionalTypesToCheck),
                _ => type.SpecialType is SpecialType.System_Collections_Generic_IEnumerable_T or SpecialType.System_Collections_IEnumerable || additionalTypesToCheck.Contains(type)
            };

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
                foreach (var methodSymbol in type.GetMembers().OfType<IMethodSymbol>())
                {
                    if (methodNames.Contains(methodSymbol.Name))
                    {
                        builder.Add(methodSymbol);
                    }
                }
            }
        }
    }
}