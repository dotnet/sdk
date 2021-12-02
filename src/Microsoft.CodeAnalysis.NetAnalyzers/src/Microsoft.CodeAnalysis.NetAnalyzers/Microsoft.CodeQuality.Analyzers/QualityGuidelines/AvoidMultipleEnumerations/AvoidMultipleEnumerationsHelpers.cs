// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    internal abstract class AvoidMultipleEnumerationsHelpers
    {
        protected abstract bool IsInvocationCausingEnumerationOverInvocationInstance(IInvocationOperation invocationOperation, WellKnownSymbolsInfo wellKnownSymbolsInfo);

        protected abstract bool IsOperationTheInstanceOfDeferredInvocation(IOperation operation, WellKnownSymbolsInfo wellKnownSymbolsInfo);

        public abstract bool IsDeferredExecutingInvocationOverInvocationInstance(IInvocationOperation invocationOperation, WellKnownSymbolsInfo wellKnownSymbolsInfo);

        /// <summary>
        /// Check if the LocalReferenceOperation or ParameterReferenceOperation is enumerated by a method invocation.
        /// </summary>
        public bool IsOperationEnumeratedByMethodInvocation(
            IOperation operation,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            RoslynDebug.Assert(operation is ILocalReferenceOperation or IParameterReferenceOperation);
            if (!IsDeferredType(operation.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            var operationToCheck = SkipDeferredAndConversionMethodIfNeeded(
                operation,
                wellKnownSymbolsInfo);
            return IsOperationEnumeratedByInvocation(operationToCheck, wellKnownSymbolsInfo);
        }

        /// <summary>
        /// Skip the deferred method call and conversion operation in linq methods call chain
        /// </summary>
        public IOperation SkipDeferredAndConversionMethodIfNeeded(
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
                return SkipDeferredAndConversionMethodIfNeeded(operation.Parent, wellKnownSymbolsInfo);
            }

            if (IsOperationTheArgumentOfDeferredInvocation(operation.Parent, wellKnownSymbolsInfo))
            {
                // This operation is used as an argument of a deferred execution method.
                // Check if the invocation of the deferred execution method is used in another deferred execution method.
                return SkipDeferredAndConversionMethodIfNeeded(operation.Parent.Parent, wellKnownSymbolsInfo);
            }

            if (IsOperationTheInstanceOfDeferredInvocation(operation, wellKnownSymbolsInfo))
            {
                return SkipDeferredAndConversionMethodIfNeeded(operation.Parent, wellKnownSymbolsInfo);
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
        public bool IsOperationEnumeratedByForEachLoop(
            IOperation operation,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            RoslynDebug.Assert(operation is ILocalReferenceOperation or IParameterReferenceOperation);
            if (!IsDeferredType(operation.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            var operationToCheck = SkipDeferredAndConversionMethodIfNeeded(operation, wellKnownSymbolsInfo);
            return operationToCheck.Parent is IForEachLoopOperation forEachLoopOperation && forEachLoopOperation.Collection == operationToCheck;
        }

        private bool IsOperationEnumeratedByInvocation(
            IOperation operation,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            if (operation.Parent is IArgumentOperation { Parent: IInvocationOperation grandParentInvocationOperation } parentArgumentOperation)
            {
                return IsInvocationCausingEnumerationOverArgument(
                    grandParentInvocationOperation,
                    parentArgumentOperation,
                    wellKnownSymbolsInfo);
            }

            if (operation.Parent is IInvocationOperation parentInvocationOperation && operation.Equals(parentInvocationOperation.Instance))
            {
                return IsInvocationCausingEnumerationOverInvocationInstance(parentInvocationOperation, wellKnownSymbolsInfo);
            }

            return false;
        }

        private static IParameterSymbol GetReducedFromParameter(IMethodSymbol methodSymbol, IParameterSymbol parameterSymbol)
        {
            RoslynDebug.Assert(methodSymbol.Parameters.Contains(parameterSymbol));
            RoslynDebug.Assert(methodSymbol.ReducedFrom != null);

            var reducedFromMethodSymbol = methodSymbol.ReducedFrom;
            var index = methodSymbol.Parameters.IndexOf(parameterSymbol);
            return reducedFromMethodSymbol.Parameters[index + 1];
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

            var targetMethod = invocationOperation.TargetMethod;
            var reduceFromMethod = targetMethod.ReducedFrom ?? targetMethod;
            var parameter = targetMethod.MethodKind == MethodKind.ReducedExtension
                ? GetReducedFromParameter(targetMethod, argumentOperationToCheck.Parameter)
                : argumentOperationToCheck.Parameter;

            // Common linq method case, like ToArray
            if (wellKnownSymbolsInfo.OneParameterEnumeratedMethods.Contains(reduceFromMethod.OriginalDefinition)
                && reduceFromMethod.IsExtensionMethod
                && !reduceFromMethod.Parameters.IsEmpty
                && parameter.Equals(reduceFromMethod.Parameters[0])
                && IsDeferredType(argumentOperationToCheck.Value.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return true;
            }

            // Example: SequentialEqual would enumerate two parameters
            if (wellKnownSymbolsInfo.TwoParametersEnumeratedMethods.Contains(reduceFromMethod.OriginalDefinition)
                && reduceFromMethod.IsExtensionMethod
                && reduceFromMethod.Parameters.Length > 1
                && (parameter.Equals(reduceFromMethod.Parameters[0]) || parameter.Equals(reduceFromMethod.Parameters[1]))
                && IsDeferredType(argumentOperationToCheck.Value.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return true;
            }

            // If the method is accepting a parameter that constrained to IEnumerable<>
            // e.g.
            // void Bar<T>(T t) where T : IEnumerable<T> { }
            // Assuming it is going to enumerate the argument
            if (HasDeferredTypeConstraint(parameter, wellKnownSymbolsInfo))
            {
                return true;
            }

            // TODO: we might want to have an attribute support here to mark a argument as 'enumerated'
            return false;
        }

        protected static bool HasDeferredTypeConstraint(IParameterSymbol parameterSymbol, WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            if (parameterSymbol.OriginalDefinition.Type is ITypeParameterSymbol typeParameterSymbol)
            {
                return typeParameterSymbol.ConstraintTypes.Any(type => IsDeferredType(type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes));
            }

            return false;
        }

        /// <summary>
        /// Check if <param name="operation"/> is an argument that passed into a deferred invocation. (like Select, Where etc.)
        /// </summary>
        private static bool IsOperationTheArgumentOfDeferredInvocation(IOperation operation, WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            return operation is IArgumentOperation { Parent: IInvocationOperation invocationParentOperation } argumentParentOperation
                && IsDeferredExecutingInvocationOverArgument(invocationParentOperation, argumentParentOperation, wellKnownSymbolsInfo);
        }

        /// <summary>
        /// Check if <param name="argumentOperationToCheck"/> is passed as a deferred executing argument into <param name="invocationOperation"/>.
        /// </summary>
        public static bool IsDeferredExecutingInvocationOverArgument(
            IInvocationOperation invocationOperation,
            IArgumentOperation argumentOperationToCheck,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            RoslynDebug.Assert(invocationOperation.Arguments.Contains(argumentOperationToCheck));
            var targetMethod = invocationOperation.TargetMethod;
            var reducedFromMethod = targetMethod.ReducedFrom ?? targetMethod;
            if (!IsDeferredType(argumentOperationToCheck.Value.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                return false;
            }

            var parameter = targetMethod.MethodKind == MethodKind.ReducedExtension
                ? GetReducedFromParameter(targetMethod, argumentOperationToCheck.Parameter)
                : argumentOperationToCheck.Parameter;
            // Method like Select, Where, etc.. only take one IEnumerable, and it is the first parameter.
            if (wellKnownSymbolsInfo.OneParameterDeferredMethods.Contains(reducedFromMethod.OriginalDefinition)
                && !reducedFromMethod.Parameters.IsEmpty
                && reducedFromMethod.Parameters[0].Equals(parameter))
            {
                return true;
            }

            // Method like Concat, Except, etc.. take two IEnumerable, and it is the first parameter or second parameter.
            if (wellKnownSymbolsInfo.TwoParametersDeferredMethods.Contains(reducedFromMethod.OriginalDefinition)
                && reducedFromMethod.Parameters.Length > 1
                && (reducedFromMethod.Parameters[0].Equals(parameter) || reducedFromMethod.Parameters[1].Equals(parameter)))
            {
                return true;
            }

            return false;
        }

        public static bool IsDeferredType(ITypeSymbol? type, ImmutableArray<ITypeSymbol> additionalTypesToCheck)
        {
            if (type == null)
            {
                return false;
            }

            return type.SpecialType is SpecialType.System_Collections_Generic_IEnumerable_T or SpecialType.System_Collections_IEnumerable
                || additionalTypesToCheck.Contains(type);
        }
    }
}