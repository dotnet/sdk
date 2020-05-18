// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
    /// <summary>
    /// CA1827: Do not use Count()/LongCount() when Any() can be used.
    /// CA1828: Do not use CountAsync()/LongCountAsync() when AnyAsync() can be used.
    /// CA1829: Use property instead of <see cref="Enumerable.Count{TSource}(System.Collections.Generic.IEnumerable{TSource})"/>, when available.
    /// CA1836: Prefer IsEmpty over Count when available.
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
        internal const string OperationKey = nameof(OperationKey);
        internal const string IsAsyncKey = nameof(IsAsyncKey);

        internal const string PropertyNameKey = nameof(PropertyNameKey);

        // CA1827
        private static readonly LocalizableString s_localizableTitle_CA1827 = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseCountWhenAnyCanBeUsedTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessag_CA1827 = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseCountWhenAnyCanBeUsedMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription_CA1827 = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseCountWhenAnyCanBeUsedDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        // CA1828
        private static readonly LocalizableString s_localizableTitle_CA1828 = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseCountAsyncWhenAnyAsyncCanBeUsedTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage_CA1828 = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseCountAsyncWhenAnyAsyncCanBeUsedMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription_CA1828 = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseCountAsyncWhenAnyAsyncCanBeUsedDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        // CA1829
        private static readonly LocalizableString s_localizableTitle_CA1829 = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UsePropertyInsteadOfCountMethodWhenAvailableTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage_CA1829 = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UsePropertyInsteadOfCountMethodWhenAvailableMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription_CA1829 = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UsePropertyInsteadOfCountMethodWhenAvailableDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        // CA1836
        private static readonly LocalizableString s_localizableTitle_CA1836 = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PreferIsEmptyOverCountTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage_CA1836 = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PreferIsEmptyOverCountMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription_CA1836 = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PreferIsEmptyOverCountDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static readonly DiagnosticDescriptor s_rule_CA1827 = DiagnosticDescriptorHelper.Create(
            CA1827,
            s_localizableTitle_CA1827,
            s_localizableMessag_CA1827,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: s_localizableDescription_CA1827,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor s_rule_CA1828 = DiagnosticDescriptorHelper.Create(
            CA1828,
            s_localizableTitle_CA1828,
            s_localizableMessage_CA1828,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: s_localizableDescription_CA1828,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor s_rule_CA1829 = DiagnosticDescriptorHelper.Create(
            CA1829,
            s_localizableTitle_CA1829,
            s_localizableMessage_CA1829,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: s_localizableDescription_CA1829,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static DiagnosticDescriptor s_rule_CA1836 = DiagnosticDescriptorHelper.Create(
            CA1836,
            s_localizableTitle_CA1836,
            s_localizableMessage_CA1836,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: s_localizableDescription_CA1836,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(
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

            INamedTypeSymbol? namedType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqEnumerable);
            IEnumerable<IMethodSymbol>? methods = namedType?.GetMembers(Count).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(syncMethods, methods);

            methods = namedType?.GetMembers(LongCount).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(syncMethods, methods);

            namedType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqQueryable);
            methods = namedType?.GetMembers(Count).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(syncMethods, methods);

            methods = namedType?.GetMembers(LongCount).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(syncMethods, methods);

            namedType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftEntityFrameworkCoreEntityFrameworkQueryableExtensions);
            methods = namedType?.GetMembers(CountAsync).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(asyncMethods, methods);

            methods = namedType?.GetMembers(LongCountAsync).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(asyncMethods, methods);

            namedType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDataEntityQueryableExtensions);
            methods = namedType?.GetMembers(CountAsync).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(asyncMethods, methods);

            methods = namedType?.GetMembers(LongCountAsync).OfType<IMethodSymbol>().Where(m => m.Parameters.Length <= 2);
            AddIfNotNull(asyncMethods, methods);

            if (syncMethods.Count > 0 || asyncMethods.Count > 0)
            {
                context.RegisterOperationAction(operationContext => AnalyzeInvocationOperation(operationContext, syncMethods.ToImmutable(), asyncMethods.ToImmutable()), OperationKind.Invocation);
            }

            context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);

            static void AddIfNotNull(ImmutableHashSet<IMethodSymbol>.Builder set, IEnumerable<IMethodSymbol>? others)
            {
                if (others != null)
                {
                    set.UnionWith(others);
                }
            }
        }

        private static void AnalyzeInvocationOperation(
            OperationAnalysisContext context, ImmutableHashSet<IMethodSymbol> syncMethods, ImmutableHashSet<IMethodSymbol> asyncMethods)
        {
            var invocationOperation = (IInvocationOperation)context.Operation;

            // For C#, TargetMethod returns the actual extension method that we are looking for.
            // For VB, TargetMethod shows as a member of the extended class, in order to get the static extension method, we need to call ReducedFrom.

            // We use OriginalDefinition to normalize the method to its generic version.
            IMethodSymbol originalDefinition = (invocationOperation.TargetMethod.ReducedFrom ?? invocationOperation.TargetMethod).OriginalDefinition;

            bool isAsync = false;

            if (!syncMethods.Contains(originalDefinition))
            {
                if (!asyncMethods.Contains(originalDefinition))
                {
                    return;
                }

                isAsync = true;
            }

            IOperation parentOperation = invocationOperation.Parent;

            if (isAsync)
            {
                // Only report awaited calls.
                if (!(parentOperation is IAwaitOperation awaitOperation))
                {
                    return;
                }

                parentOperation = awaitOperation.Parent;
            }

            parentOperation = parentOperation.WalkUpParentheses();
            parentOperation = parentOperation.WalkUpConversion();

            // Analyze binary operation.
            if (parentOperation is IBinaryOperation parentBinaryOperation)
            {
                AnalyzeParentBinaryOperation(context, invocationOperation, parentBinaryOperation,
                    isAsync, allowLinq: true, originalDefinition.Name);
            }
            // Analyze invocation operation, potentially obj.Count().Equals(0).
            else if (parentOperation is IInvocationOperation parentInvocationOperation)
            {
                AnalyzeParentInvocationOperation(context, invocationOperation, parentInvocationOperation,
                    isInstance: true, isAsync, allowLinq: true, originalDefinition.Name);
            }
            // Analyze argument operation, potentially 0.Equals(obj.Count()).
            else if (parentOperation is IArgumentOperation argumentOperation)
            {
                AnalyzeParentInvocationOperation(context, invocationOperation, (IInvocationOperation)argumentOperation.Parent,
                    isInstance: false, isAsync, allowLinq: true, originalDefinition.Name);
            }
            // Standalone call to Count(), potentially i = obj.Count(); we can't replace with Any() or IsEmpty but we can lookup if property Count or Lengh can be used instead.
            else if (!isAsync)
            {
                AnalyzeCountInvocationOperation(context, invocationOperation);
            }
        }

        private static void AnalyzePropertyReference(OperationAnalysisContext context)
        {
            var propertyReferenceOperation = (IPropertyReferenceOperation)context.Operation;
            IOperation parentOperation = propertyReferenceOperation.Parent;

            parentOperation = parentOperation.WalkUpParentheses();
            parentOperation = parentOperation.WalkUpConversion();

            // Analyze binary operation.
            if (parentOperation is IBinaryOperation parentBinaryOperation)
            {
                AnalyzeParentBinaryOperation(context, propertyReferenceOperation, parentBinaryOperation,
                    isAsync: false, allowLinq: false, methodName: null);
            }
            // Analyze invocation operation, potentially obj.Count.Equals(0).
            else if (parentOperation is IInvocationOperation parentInvocationOperation)
            {
                AnalyzeParentInvocationOperation(context, propertyReferenceOperation, parentInvocationOperation,
                    isInstance: true, isAsync: false, allowLinq: false, methodName: null);
            }
            // Analyze argument operation, potentially 0.Equals(obj.Count).
            else if (parentOperation is IArgumentOperation argumentOperation)
            {
                AnalyzeParentInvocationOperation(context, propertyReferenceOperation, (IInvocationOperation)argumentOperation.Parent,
                    isInstance: false, isAsync: false, allowLinq: false, methodName: null);
            }
        }

        private static void AnalyzeParentBinaryOperation(
            OperationAnalysisContext context, IOperation operation, IBinaryOperation parent, bool isAsync, bool allowLinq, string? methodName)
        {
            bool useRightSideExpression = default;
            if (!IsLeftCountComparison(parent, out bool shouldNegateIsEmpty))
            {
                if (!IsRightCountComparison(parent, out shouldNegateIsEmpty))
                {
                    return;
                }

                useRightSideExpression = true;
            }

            if (allowLinq && isAsync)
            {
                bool shouldNegateAny = !shouldNegateIsEmpty;
                ReportCA1828(context, shouldNegateAny, useRightSideExpression ? OperationBinaryRight : OperationBinaryLeft, methodName!, parent);
            }
            else
            {
                ITypeSymbol? type = operation.GetInstanceType();

                if (type != null && TypeContainsVsibileProperty(context, type, IsEmpty, SpecialType.System_Boolean))
                {
                    ReportCA1836(context, useRightSideExpression, shouldNegateIsEmpty, parent);
                }
                else if (allowLinq)
                {
                    bool shouldNegateAny = !shouldNegateIsEmpty;
                    ReportCA1827(context, shouldNegateAny, useRightSideExpression ? OperationBinaryRight : OperationBinaryLeft, methodName!, parent);
                }
            }
        }

        private static void AnalyzeParentInvocationOperation(
            OperationAnalysisContext context, IOperation operation, IInvocationOperation parent, bool isInstance, bool isAsync, bool allowLinq, string? methodName)
        {
            if (!IsIntEqualsMethod(parent))
            {
                return;
            }

            if (isInstance)
            {
                if (!TryGetZeroOrOneConstant(parent.Arguments[0].Value, out var constant) || constant != 0)
                {
                    return;
                }
            }
            else if (!TryGetZeroOrOneConstant(parent.Instance, out var constant) || constant != 0)
            {
                return;
            }

            if (allowLinq && isAsync)
            {
                ReportCA1828(context, shouldNegate: true, isInstance ? OperationEqualsInstance : OperationEqualsArgument, methodName!, parent);
            }
            else
            {
                ITypeSymbol? type = operation.GetInstanceType();

                if (type != null && TypeContainsVsibileProperty(context, type, IsEmpty, SpecialType.System_Boolean))
                {
                    ReportCA1836(context, !isInstance, shouldNegate: false, parent);
                }
                else if (allowLinq)
                {
                    ReportCA1827(context, shouldNegate: true, isInstance ? OperationEqualsInstance : OperationEqualsArgument, methodName!, parent);
                }
            }
        }

        private static void AnalyzeCountInvocationOperation(OperationAnalysisContext context, IInvocationOperation invocationOperation)
        {
            ITypeSymbol? type = invocationOperation.GetInstanceType();

            string propertyName = Length;
            if (type != null && !TypeContainsVsibileProperty(context, type, propertyName, SpecialType.System_Int32, SpecialType.System_UInt64))
            {
                propertyName = Count;
                if (!TypeContainsVsibileProperty(context, type, propertyName, SpecialType.System_Int32, SpecialType.System_UInt64))
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

        private static void ReportCA1836(OperationAnalysisContext context, bool useRightSideExpression, bool shouldNegate, IOperation operation)
        {
            ImmutableDictionary<string, string?>.Builder propertiesBuilder = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);

            if (useRightSideExpression)
            {
                propertiesBuilder.Add(UseRightSideExpressionKey, null);
            }

            if (shouldNegate)
            {
                propertiesBuilder.Add(ShouldNegateKey, null);
            }

            context.ReportDiagnostic(
                operation.Syntax.CreateDiagnostic(
                    rule: s_rule_CA1836,
                    properties: propertiesBuilder.ToImmutable()));
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

        private static bool TypeContainsVsibileProperty(OperationAnalysisContext context, ITypeSymbol type, string propertyName, SpecialType propertyType)
            => TypeContainsVsibileProperty(context, type, propertyName, propertyType, propertyType);

        private static bool TypeContainsVsibileProperty(OperationAnalysisContext context, ITypeSymbol type, string propertyName, SpecialType lowerBound, SpecialType upperBound)
        {
            if (TypeContainsMember(context, type, propertyName, lowerBound, upperBound, out bool isPropertyValidAndVisible))
            {
                return isPropertyValidAndVisible;
            }

            // The property might not be defined on the specified type if the type is an interface, it can be defined in one of the parent interfaces.
            if (type.TypeKind == TypeKind.Interface)
            {
                foreach (var @interface in type.AllInterfaces)
                {
                    if (TypeContainsMember(context, @interface, propertyName, lowerBound, upperBound, out isPropertyValidAndVisible))
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
                    if (TypeContainsMember(context, currentType, propertyName, lowerBound, upperBound, out isPropertyValidAndVisible))
                    {
                        return isPropertyValidAndVisible;
                    }

                    currentType = currentType.BaseType;
                }
            }

            return false;

            static bool TypeContainsMember(
                OperationAnalysisContext context, ITypeSymbol type, string propertyName, SpecialType lowerBound, SpecialType upperBound, out bool isPropertyValidAndVisible)
            {
                if (type.GetMembers(propertyName).FirstOrDefault() is IPropertySymbol property)
                {
                    isPropertyValidAndVisible = !property.IsStatic &&
                        IsInRangeInclusive((uint)property.Type.SpecialType, (uint)lowerBound, (uint)upperBound) &&
                        property.GetMethod != null &&
                        context.Compilation.IsSymbolAccessibleWithin(property, context.ContainingSymbol.ContainingType) &&
                        context.Compilation.IsSymbolAccessibleWithin(property.GetMethod, context.ContainingSymbol.ContainingType);

                    return true;
                }

                isPropertyValidAndVisible = default;
                return false;
            }
        }

        private static bool TryGetZeroOrOneConstant(IOperation operation, out int constant)
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

            return constant == 0 || constant == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInRangeInclusive(uint value, uint lowerBound, uint upperBound)
            => (value - lowerBound) <= (upperBound - lowerBound);
    }
}
