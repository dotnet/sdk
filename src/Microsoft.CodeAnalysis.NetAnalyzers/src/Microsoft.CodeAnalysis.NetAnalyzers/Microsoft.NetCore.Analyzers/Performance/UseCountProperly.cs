﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1827: <inheritdoc cref="DoNotUseCountWhenAnyCanBeUsedTitle"/>
    /// CA1828: <inheritdoc cref="DoNotUseCountAsyncWhenAnyAsyncCanBeUsedTitle"/>
    /// CA1829: <inheritdoc cref="UsePropertyInsteadOfCountMethodWhenAvailableTitle"/>
    /// CA1836: <inheritdoc cref="PreferIsEmptyOverCountTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseCountProperlyAnalyzer : DiagnosticAnalyzer
    {
        internal const string CA1827 = nameof(CA1827);
        internal const string CA1828 = nameof(CA1828);
        internal const string CA1829 = nameof(CA1829);
        internal const string CA1836 = nameof(CA1836);

        private const string Length = nameof(Length);
        private const string Count = nameof(Count);
        private const string LongCount = nameof(LongCount);
        private const string CountAsync = nameof(CountAsync);
        private const string LongCountAsync = nameof(LongCountAsync);

        internal const string UseRightSideExpressionKey = nameof(UseRightSideExpressionKey);
        internal const string ShouldNegateKey = nameof(ShouldNegateKey);
        internal const string IsEmpty = nameof(IsEmpty);

        internal const string OperationEqualsInstance = nameof(OperationEqualsInstance);
        internal const string OperationEqualsArgument = nameof(OperationEqualsArgument);
        internal const string OperationBinaryLeft = nameof(OperationBinaryLeft);
        internal const string OperationBinaryRight = nameof(OperationBinaryRight);
        internal const string OperationIsPattern = nameof(OperationIsPattern);
        internal const string OperationKey = nameof(OperationKey);
        internal const string IsAsyncKey = nameof(IsAsyncKey);

        internal const string PropertyNameKey = nameof(PropertyNameKey);

        internal static readonly DiagnosticDescriptor s_rule_CA1827 = DiagnosticDescriptorHelper.Create(
            CA1827,
            CreateLocalizableResourceString(nameof(DoNotUseCountWhenAnyCanBeUsedTitle)),
            CreateLocalizableResourceString(nameof(DoNotUseCountWhenAnyCanBeUsedMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(DoNotUseCountWhenAnyCanBeUsedDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor s_rule_CA1828 = DiagnosticDescriptorHelper.Create(
            CA1828,
            CreateLocalizableResourceString(nameof(DoNotUseCountAsyncWhenAnyAsyncCanBeUsedTitle)),
            CreateLocalizableResourceString(nameof(DoNotUseCountAsyncWhenAnyAsyncCanBeUsedMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(DoNotUseCountAsyncWhenAnyAsyncCanBeUsedDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor s_rule_CA1829 = DiagnosticDescriptorHelper.Create(
            CA1829,
            CreateLocalizableResourceString(nameof(UsePropertyInsteadOfCountMethodWhenAvailableTitle)),
            CreateLocalizableResourceString(nameof(UsePropertyInsteadOfCountMethodWhenAvailableMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(UsePropertyInsteadOfCountMethodWhenAvailableDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor s_rule_CA1836 = DiagnosticDescriptorHelper.Create(
            CA1836,
            CreateLocalizableResourceString(nameof(PreferIsEmptyOverCountTitle)),
            CreateLocalizableResourceString(nameof(PreferIsEmptyOverCountMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(PreferIsEmptyOverCountDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
            = ImmutableArray.Create(
                s_rule_CA1827,
                s_rule_CA1828,
                s_rule_CA1829,
                s_rule_CA1836);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            ImmutableHashSet<IMethodSymbol>.Builder syncMethods = ImmutableHashSet.CreateBuilder<IMethodSymbol>();
            ImmutableHashSet<IMethodSymbol>.Builder asyncMethods = ImmutableHashSet.CreateBuilder<IMethodSymbol>();

            WellKnownTypeProvider typeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
            INamedTypeSymbol? namedType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqEnumerable);
            IEnumerable<IMethodSymbol>? methods = namedType?.GetMembers(Count).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(syncMethods, methods);

            methods = namedType?.GetMembers(LongCount).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(syncMethods, methods);

            namedType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqQueryable);
            methods = namedType?.GetMembers(Count).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(syncMethods, methods);

            methods = namedType?.GetMembers(LongCount).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(syncMethods, methods);

            namedType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftEntityFrameworkCoreEntityFrameworkQueryableExtensions);
            methods = namedType?.GetMembers(CountAsync).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(asyncMethods, methods);

            methods = namedType?.GetMembers(LongCountAsync).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(asyncMethods, methods);

            namedType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDataEntityQueryableExtensions);
            methods = namedType?.GetMembers(CountAsync).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(asyncMethods, methods);

            methods = namedType?.GetMembers(LongCountAsync).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(asyncMethods, methods);

            // Allowed types that should report a CA1836 diagnosis given that there is proven benefit on doing so.
            ImmutableHashSet<ITypeSymbol>.Builder allowedTypesBuilder = ImmutableHashSet.CreateBuilder<ITypeSymbol>();

            namedType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsConcurrentConcurrentBag1);
            allowedTypesBuilder.AddIfNotNull(namedType);

            namedType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsConcurrentConcurrentDictionary2);
            allowedTypesBuilder.AddIfNotNull(namedType);

            namedType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsConcurrentConcurrentQueue1);
            allowedTypesBuilder.AddIfNotNull(namedType);

            namedType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsConcurrentConcurrentStack1);
            allowedTypesBuilder.AddIfNotNull(namedType);

            ImmutableHashSet<ITypeSymbol> allowedTypesForCA1836 = allowedTypesBuilder.ToImmutable();

            var linqExpressionType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqExpressionsExpression1);

            if (syncMethods.Count > 0 || asyncMethods.Count > 0)
            {
                context.RegisterOperationAction(operationContext => AnalyzeInvocationOperation(
                    operationContext, linqExpressionType, syncMethods.ToImmutable(), asyncMethods.ToImmutable(), allowedTypesForCA1836),
                    OperationKind.Invocation);
            }

            if (!allowedTypesForCA1836.IsEmpty)
            {
                context.RegisterOperationAction(operationContext => AnalyzePropertyReference(
                    operationContext, linqExpressionType, allowedTypesForCA1836),
                    OperationKind.PropertyReference);
            }

            static void AddIfNotNull(ImmutableHashSet<IMethodSymbol>.Builder set, IEnumerable<IMethodSymbol>? others)
            {
                if (others != null)
                {
                    set.UnionWith(others);
                }
            }
        }

        private static void AnalyzeInvocationOperation(
            OperationAnalysisContext context,
            INamedTypeSymbol? linqExpressionType,
            ImmutableHashSet<IMethodSymbol> syncMethods,
            ImmutableHashSet<IMethodSymbol> asyncMethods,
            ImmutableHashSet<ITypeSymbol> allowedTypesForCA1836)
        {
            var invocationOperation = (IInvocationOperation)context.Operation;

            // For C#, TargetMethod returns the actual extension method that we are looking for.
            // For VB, TargetMethod shows as a member of the extended class, in order to get the static extension method, we need to call ReducedFrom.

            // We use OriginalDefinition to normalize the method to its generic version.
            IMethodSymbol originalDefinition = (invocationOperation.TargetMethod.ReducedFrom ?? invocationOperation.TargetMethod).OriginalDefinition;

            bool isAsync = false;
            bool hasPredicate = originalDefinition.Parameters.Length > 1;

            if (!syncMethods.Contains(originalDefinition))
            {
                if (!asyncMethods.Contains(originalDefinition))
                {
                    return;
                }

                isAsync = true;
            }

            IOperation? parentOperation = invocationOperation.WalkUpParentheses().WalkUpConversion().Parent;

            if (isAsync)
            {
                // Only report awaited calls.
                if (parentOperation is not IAwaitOperation awaitOperation)
                {
                    return;
                }

                parentOperation = awaitOperation.WalkUpParentheses().WalkUpConversion().Parent;
            }

            bool shouldReplaceParent = ShouldReplaceParent(ref parentOperation, out string? operationKey, out bool shouldNegateIsEmpty);

            // Check if we are in a Expression<Func<T...>> context, in which case it is possible
            // that the underlying call doesn't have the Count/Length property so we want to bail-out.
            var isWithinExpressionTree = invocationOperation.IsWithinExpressionTree(linqExpressionType);

            DetermineReportForInvocationAnalysis(context, invocationOperation, parentOperation,
                shouldReplaceParent, isAsync, shouldNegateIsEmpty, hasPredicate, isWithinExpressionTree,
                originalDefinition.Name, operationKey, allowedTypesForCA1836);
        }

        private static void AnalyzePropertyReference(OperationAnalysisContext context, INamedTypeSymbol? linqExpressionType,
            ImmutableHashSet<ITypeSymbol> allowedTypesForCA1836)
        {
            var propertyReferenceOperation = (IPropertyReferenceOperation)context.Operation;

            string propertyName = propertyReferenceOperation.Member.Name;
            if (propertyName is not Count and not Length ||
                propertyReferenceOperation.IsWithinExpressionTree(linqExpressionType))
            {
                return;
            }

            IOperation? parentOperation = propertyReferenceOperation.WalkUpParentheses().WalkUpConversion().Parent;

            bool shouldReplaceParent = ShouldReplaceParent(ref parentOperation, out string? operationKey, out bool shouldNegateIsEmpty);

            if (shouldReplaceParent)
            {
                DetermineReportForPropertyReference(context, propertyReferenceOperation, parentOperation!,
                    operationKey, shouldNegateIsEmpty, allowedTypesForCA1836);
            }
        }

        private static bool ShouldReplaceParent(ref IOperation? parentOperation, out string? operationKey, out bool shouldNegateIsEmpty)
        {
            bool shouldReplace = false;
            shouldNegateIsEmpty = false;
            operationKey = null;

            // Analyze binary operation.
            if (parentOperation is IBinaryOperation parentBinaryOperation)
            {
                shouldReplace = AnalyzeParentBinaryOperation(parentBinaryOperation, out bool useRightSide, out shouldNegateIsEmpty);
                operationKey = useRightSide ? OperationBinaryRight : OperationBinaryLeft;
            }
            // Analyze invocation operation, potentially obj.Count.Equals(0).
            else if (parentOperation is IInvocationOperation parentInvocationOperation)
            {
                shouldReplace = AnalyzeParentInvocationOperation(parentInvocationOperation, isInstance: true);
                operationKey = OperationEqualsInstance;
            }
            // Analyze argument operation, potentially 0.Equals(obj.Count).
            else if (parentOperation is IArgumentOperation argumentOperation &&
                argumentOperation.Parent is IInvocationOperation argumentParentInvocationOperation)
            {
                parentOperation = argumentParentInvocationOperation;
                shouldReplace = AnalyzeParentInvocationOperation(argumentParentInvocationOperation, isInstance: false);
                operationKey = OperationEqualsArgument;
            }
            else if (parentOperation is IIsPatternOperation isPatternOperation)
            {
                shouldReplace = AnalyzeIsPatternOperation(isPatternOperation, out shouldNegateIsEmpty);
                operationKey = OperationIsPattern;
            }

            return shouldReplace;
        }

        private static bool AnalyzeParentBinaryOperation(IBinaryOperation parent, out bool useRightSide, out bool shouldNegate)
        {
            useRightSide = default;
            if (!IsLeftCountComparison(parent, out shouldNegate))
            {
                if (!IsRightCountComparison(parent, out shouldNegate))
                {
                    return false;
                }

                useRightSide = true;
            }

            return true;
        }

        private static bool AnalyzeParentInvocationOperation(IInvocationOperation parent, bool isInstance)
        {
            if (!IsIntEqualsMethod(parent))
            {
                return false;
            }

            IOperation? constantOperation = isInstance ? parent.Arguments[0].Value : parent.Instance;
            if (!TryGetZeroOrOneConstant(constantOperation, out int constant) || constant != 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Analyzes different patterns of ".Count() is …".
        /// </summary>
        /// <param name="isPatternOperation">The pattern.</param>
        /// <param name="shouldNegateIsEmpty">
        /// Determines if the resulting rewritten call to ".Any()" should be negated.
        /// When we want the call to be negated, we set 'shouldNegateIsEmpty' to 'false', since 'shouldNegateIsEmpty' = '!shouldNegateAny'.
        /// This negation happens before 'ReportCA1827()' is called.
        /// </param>
        /// <returns></returns>
        private static bool AnalyzeIsPatternOperation(IIsPatternOperation isPatternOperation, out bool shouldNegateIsEmpty)
        {
            shouldNegateIsEmpty = true;

            // Handles ".Count() is not …" patterns.
            if (isPatternOperation.Pattern is INegatedPatternOperation negatedPattern)
            {
                // Handles the ".Count() is not > 0" and ".Count() is not >= 1" patterns.
                if (negatedPattern.Pattern is IRelationalPatternOperation { OperatorKind: BinaryOperatorKind.GreaterThan, Value: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } } }
                    or IRelationalPatternOperation { OperatorKind: BinaryOperatorKind.GreaterThanOrEqual, Value: ILiteralOperation { ConstantValue: { HasValue: true, Value: 1 } } })
                {
                    shouldNegateIsEmpty = false;

                    return true;
                }

                // Handles the ".Count() is not 0" and ".Count() is not <= 0" patterns.
                return negatedPattern.Pattern is IConstantPatternOperation { Value: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } } }
                    or IRelationalPatternOperation { OperatorKind: BinaryOperatorKind.LessThanOrEqual, Value: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } } };
            }

            // Handles the ".Count() is 0" pattern.
            if (isPatternOperation.Pattern is IConstantPatternOperation { Value: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } } })
            {
                shouldNegateIsEmpty = false;

                return true;
            }

            // Handles the ".Count() </<=/>/>= …" patterns.
            if (isPatternOperation.Pattern is IRelationalPatternOperation relationalPattern)
            {
                // Handles the ".Count() is < 1" and ".Count() is <= 0" patterns.
                if (relationalPattern is { OperatorKind: BinaryOperatorKind.LessThan, Value: ILiteralOperation { ConstantValue: { HasValue: true, Value: 1 } } }
                    or { OperatorKind: BinaryOperatorKind.LessThanOrEqual, Value: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } } })
                {
                    shouldNegateIsEmpty = false;

                    return true;
                }

                // Handles the ".Count() is > 0" and ".Count() is >= 1" patterns.
                return relationalPattern is { OperatorKind: BinaryOperatorKind.GreaterThan, Value: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } } }
                    or { OperatorKind: BinaryOperatorKind.GreaterThanOrEqual, Value: ILiteralOperation { ConstantValue: { HasValue: true, Value: 1 } } };
            }

            return false;
        }

        private static void AnalyzeCountInvocationOperation(OperationAnalysisContext context, IInvocationOperation invocationOperation)
        {
            ITypeSymbol? type = invocationOperation.GetInstanceType();

            string propertyName = Length;
            if (type != null && !TypeContainsVisibleProperty(context, type, propertyName, SpecialType.System_Int32, SpecialType.System_UInt64, out _))
            {
                propertyName = Count;
                if (!TypeContainsVisibleProperty(context, type, propertyName, SpecialType.System_Int32, SpecialType.System_UInt64, out _))
                {
                    return;
                }
            }

            ReportCA1829(context, propertyName, invocationOperation);
        }

        private static void ReportCA1827(OperationAnalysisContext context, bool shouldNegate, string operationKey, string methodName, IOperation operation)
        {
            ImmutableDictionary<string, string?>.Builder propertiesBuilder = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);
            propertiesBuilder.Add(OperationKey, operationKey);

            if (shouldNegate)
            {
                propertiesBuilder.Add(ShouldNegateKey, null);
            }

            context.ReportDiagnostic(
                operation.Syntax.CreateDiagnostic(
                    rule: s_rule_CA1827,
                    properties: propertiesBuilder.ToImmutable(),
                    args: methodName));
        }

        private static void ReportCA1828(OperationAnalysisContext context, bool shouldNegate, string operationKey, string methodName, IOperation operation)
        {
            ImmutableDictionary<string, string?>.Builder propertiesBuilder = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);
            propertiesBuilder.Add(OperationKey, operationKey);
            propertiesBuilder.Add(IsAsyncKey, null);

            if (shouldNegate)
            {
                propertiesBuilder.Add(ShouldNegateKey, null);
            }

            context.ReportDiagnostic(
                operation.Syntax.CreateDiagnostic(
                    rule: s_rule_CA1828,
                    properties: propertiesBuilder.ToImmutable(),
                    args: methodName));
        }

        private static void ReportCA1829(OperationAnalysisContext context, string propertyName, IOperation operation)
        {
            ImmutableDictionary<string, string?>.Builder propertiesBuilder = ImmutableDictionary.CreateBuilder<string, string?>();
            propertiesBuilder.Add(PropertyNameKey, propertyName);

            context.ReportDiagnostic(
                operation.Syntax.CreateDiagnostic(
                    rule: s_rule_CA1829,
                    properties: propertiesBuilder.ToImmutable(),
                    propertyName));
        }

        private static void ReportCA1836(OperationAnalysisContext context, string operationKey, bool shouldNegate, IOperation operation)
        {
            ImmutableDictionary<string, string?>.Builder propertiesBuilder = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);
            propertiesBuilder.Add(OperationKey, operationKey);

            if (shouldNegate)
            {
                propertiesBuilder.Add(ShouldNegateKey, null);
            }

            context.ReportDiagnostic(
                operation.Syntax.CreateDiagnostic(
                    rule: s_rule_CA1836,
                    properties: propertiesBuilder.ToImmutable()));
        }

        private static void DetermineReportForInvocationAnalysis(
            OperationAnalysisContext context,
            IInvocationOperation invocationOperation, IOperation? parent,
            bool shouldReplaceParent, bool isAsync, bool shouldNegateIsEmpty, bool hasPredicate, bool isWithinExpressionTree,
            string methodName, string? operationKey,
            ImmutableHashSet<ITypeSymbol> allowedTypesForCA1836)
        {
            if (!shouldReplaceParent)
            {
                // Parent expression doesn't met the criteria to be replaced; fallback on analysis for standalone call to Count().
                if (!isAsync && !hasPredicate && !isWithinExpressionTree)
                {
                    AnalyzeCountInvocationOperation(context, invocationOperation);
                }

                return;
            }

            RoslynDebug.Assert(parent != null);

            if (isAsync)
            {
                bool shouldNegateAny = !shouldNegateIsEmpty;
                ReportCA1828(context, shouldNegateAny, operationKey!, methodName, parent);
            }
            else if (!isWithinExpressionTree)
            {
                ITypeSymbol? type = invocationOperation.GetInstanceType();
                if (type == null)
                {
                    return;
                }

                // If the invocation has a predicate we can only suggest CA1827;
                // otherwise, we would loose the lambda if we suggest CA1829 or CA1836.
                if (hasPredicate)
                {
                    bool shouldNegateAny = !shouldNegateIsEmpty;
                    ReportCA1827(context, shouldNegateAny, operationKey!, methodName, parent);
                }
                else if (allowedTypesForCA1836.Contains(type.OriginalDefinition) &&
                   TypeContainsVisibleProperty(context, type, IsEmpty, SpecialType.System_Boolean, out ISymbol? isEmptyPropertySymbol) &&
                   !IsPropertyGetOfIsEmptyUsingThisInstance(context, invocationOperation, isEmptyPropertySymbol!))
                {
                    ReportCA1836(context, operationKey!, shouldNegateIsEmpty, parent);
                }
                else if (TypeContainsVisibleProperty(context, type, Length, SpecialType.System_Int32, SpecialType.System_UInt64, out _))
                {
                    ReportCA1829(context, Length, invocationOperation);
                }
                else if (TypeContainsVisibleProperty(context, type, Count, SpecialType.System_Int32, SpecialType.System_UInt64, out _))
                {
                    ReportCA1829(context, Count, invocationOperation);
                }
                else
                {
                    bool shouldNegateAny = !shouldNegateIsEmpty;
                    ReportCA1827(context, shouldNegateAny, operationKey!, methodName, parent);
                }
            }
        }

        private static void DetermineReportForPropertyReference(
            OperationAnalysisContext context, IOperation? operation, IOperation parent,
            string? operationKey, bool shouldNegateIsEmpty,
            ImmutableHashSet<ITypeSymbol> allowedTypesForCA1836)
        {
            ITypeSymbol? type = operation?.GetInstanceType();
            if (type != null)
            {
                if (allowedTypesForCA1836.Contains(type.OriginalDefinition) &&
                    TypeContainsVisibleProperty(context, type, IsEmpty, SpecialType.System_Boolean, out ISymbol? isEmptyPropertySymbol) &&
                    !IsPropertyGetOfIsEmptyUsingThisInstance(context, operation, isEmptyPropertySymbol!))
                {
                    ReportCA1836(context, operationKey!, shouldNegateIsEmpty, parent);
                }
            }
        }

        /// <summary>
        /// Checks if the given methods is the <see cref="int.Equals(int)"/> methods.
        /// </summary>
        /// <param name="invocationOperation">The invocation operation.</param>
        /// <returns><see langword="true"/> if the given methods is the <see cref="int.Equals(int)"/> methods; otherwise, <see langword="false"/>.</returns>
        private static bool IsIntEqualsMethod(IInvocationOperation invocationOperation)
        {
            if (invocationOperation.Arguments.Length != 1)
            {
                return false;
            }

            IMethodSymbol methodSymbol = invocationOperation.TargetMethod;

            return string.Equals(methodSymbol.Name, WellKnownMemberNames.ObjectEquals, StringComparison.Ordinal)
                && IsInRangeInclusive((uint)methodSymbol.ContainingType.SpecialType, (uint)SpecialType.System_Int32, (uint)SpecialType.System_UInt64);
        }

        private static bool IsLeftCountComparison(IBinaryOperation binaryOperation, out bool shouldNegate)
        {
            shouldNegate = false;

            if (!TryGetZeroOrOneConstant(binaryOperation.RightOperand, out int constant))
            {
                return false;
            }

            switch (constant)
            {
                case 0:
                    switch (binaryOperation.OperatorKind)
                    {
                        case BinaryOperatorKind.Equals:
                        case BinaryOperatorKind.LessThanOrEqual:
                            shouldNegate = false;
                            break;
                        case BinaryOperatorKind.NotEquals:
                        case BinaryOperatorKind.GreaterThan:
                            shouldNegate = true;
                            break;
                        default:
                            return false;
                    }

                    break;
                case 1:
                    switch (binaryOperation.OperatorKind)
                    {
                        case BinaryOperatorKind.LessThan:
                            shouldNegate = false;
                            break;
                        case BinaryOperatorKind.GreaterThanOrEqual:
                            shouldNegate = true;
                            break;
                        default:
                            return false;
                    }

                    break;
                default:
                    return false;
            }

            return true;
        }

        private static bool IsRightCountComparison(IBinaryOperation binaryOperation, out bool shouldNegate)
        {
            shouldNegate = false;

            if (!TryGetZeroOrOneConstant(binaryOperation.LeftOperand, out int constant))
            {
                return false;
            }

            switch (constant)
            {
                case 0:
                    switch (binaryOperation.OperatorKind)
                    {
                        case BinaryOperatorKind.Equals:
                        case BinaryOperatorKind.GreaterThanOrEqual:
                            shouldNegate = false;
                            break;

                        case BinaryOperatorKind.LessThan:
                        case BinaryOperatorKind.NotEquals:
                            shouldNegate = true;
                            break;

                        default:
                            return false;
                    }

                    break;
                case 1:
                    switch (binaryOperation.OperatorKind)
                    {
                        case BinaryOperatorKind.LessThanOrEqual:
                            shouldNegate = true;
                            break;

                        case BinaryOperatorKind.GreaterThan:
                            shouldNegate = false;
                            break;

                        default:
                            return false;
                    }

                    break;
                default:
                    return false;
            }

            return true;
        }

        private static bool TypeContainsVisibleProperty(OperationAnalysisContext context, ITypeSymbol type, string propertyName, SpecialType propertyType, out ISymbol? propertySymbol)
            => TypeContainsVisibleProperty(context, type, propertyName, propertyType, propertyType, out propertySymbol);

        private static bool TypeContainsVisibleProperty(OperationAnalysisContext context, ITypeSymbol type, string propertyName, SpecialType lowerBound, SpecialType upperBound, out ISymbol? propertySymbol)
        {
            if (TypeContainsMember(context, type, propertyName, lowerBound, upperBound, out bool isPropertyValidAndVisible, out propertySymbol!))
            {
                return isPropertyValidAndVisible;
            }

            // The property might not be defined on the specified type if the type is an interface, it can be defined in one of the parent interfaces.
            if (type.TypeKind == TypeKind.Interface)
            {
                foreach (var @interface in type.AllInterfaces)
                {
                    if (TypeContainsMember(context, @interface, propertyName, lowerBound, upperBound, out isPropertyValidAndVisible, out propertySymbol))
                    {
                        return isPropertyValidAndVisible;
                    }
                }
            }
            else
            {
                ITypeSymbol? currentType = type.BaseType;
                while (currentType != null)
                {
                    if (TypeContainsMember(context, currentType, propertyName, lowerBound, upperBound, out isPropertyValidAndVisible, out propertySymbol))
                    {
                        return isPropertyValidAndVisible;
                    }

                    currentType = currentType.BaseType;
                }
            }

            return false;

            static bool TypeContainsMember(
                OperationAnalysisContext context, ITypeSymbol type, string propertyName, SpecialType lowerBound, SpecialType upperBound,
                out bool isPropertyValidAndVisible, out ISymbol? propertySymbol)
            {
                if (type.GetMembers(propertyName).FirstOrDefault() is IPropertySymbol property)
                {
                    isPropertyValidAndVisible = !property.IsStatic &&
                        IsInRangeInclusive((uint)property.Type.SpecialType, (uint)lowerBound, (uint)upperBound) &&
                        property.GetMethod != null &&
                        context.Compilation.IsSymbolAccessibleWithin(property, context.ContainingSymbol.ContainingType) &&
                        context.Compilation.IsSymbolAccessibleWithin(property.GetMethod, context.ContainingSymbol.ContainingType);

                    propertySymbol = property;

                    return true;
                }

                isPropertyValidAndVisible = default;
                propertySymbol = default;

                return false;
            }
        }

        private static bool TryGetZeroOrOneConstant(IOperation? operation, out int constant)
        {
            constant = default;

            switch (operation?.Type?.SpecialType)
            {
                case SpecialType.System_Int32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt32:
                case SpecialType.System_UInt64:
                case SpecialType.System_Object:
                    break;

                default:
                    return false;
            }

            operation = operation.WalkDownConversion();

            var comparandValueOpt = operation.ConstantValue;

            if (!comparandValueOpt.HasValue)
            {
                return false;
            }

            constant = comparandValueOpt.Value switch
            {
                int intValue => intValue,
                uint uintValue => (int)uintValue,
                long longValue => (int)longValue,
                ulong ulongValue => (int)ulongValue,
                _ => -1
            };

            return constant is 0 or 1;
        }

        private static bool IsPropertyGetOfIsEmptyUsingThisInstance(OperationAnalysisContext context, IOperation? operation, ISymbol isEmptyPropertySymbol)
        {
            ISymbol containingSymbol = context.ContainingSymbol;

            return containingSymbol is IMethodSymbol methodSymbol &&
                // Is within the body of a property getter?
                methodSymbol.MethodKind == MethodKind.PropertyGet &&
                // Is the getter of the IsEmpty property.
                methodSymbol.AssociatedSymbol == isEmptyPropertySymbol &&
                // Is 'this' instance?
                operation?.GetInstanceType() == containingSymbol.ContainingType;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInRangeInclusive(uint value, uint lowerBound, uint upperBound)
            => (value - lowerBound) <= (upperBound - lowerBound);
    }
}