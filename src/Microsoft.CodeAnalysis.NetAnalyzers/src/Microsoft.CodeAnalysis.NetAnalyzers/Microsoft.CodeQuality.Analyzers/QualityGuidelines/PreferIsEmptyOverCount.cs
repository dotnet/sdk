// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA1836: Prefer IsEmpty over Count when available.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferIsEmptyOverCountAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1836";
        internal const string UseRightSideExpressionKey = nameof(UseRightSideExpressionKey);
        internal const string ShouldNegateKey = nameof(ShouldNegateKey);
        internal const string IsEmpty = nameof(IsEmpty);
        private const string Count = nameof(Count);

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PreferIsEmptyOverCountTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PreferIsEmptyOverCountMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PreferIsEmptyOverCountDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.IdeSuggestion,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: false,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterOperationAction(AnalyzeBynaryOperation, OperationKind.BinaryOperator);
        }

        private static void AnalyzeBynaryOperation(OperationAnalysisContext context)
        {
            var binaryOperation = (IBinaryOperation)context.Operation;

            if (binaryOperation.IsComparisonOperator())
            {
                bool useRightSideExpression = false;

                if (!IsLeftCountComparison(binaryOperation, out ITypeSymbol? containingType, out bool shouldNegate))
                {
                    if (!IsRightCountComparison(binaryOperation, out containingType, out shouldNegate))
                    {
                        return;
                    }

                    useRightSideExpression = true;
                }

                if (!TypeContainsValidIsEmptyProperty(containingType, context))
                {
                    return;
                }

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
                    binaryOperation.Syntax.CreateDiagnostic(
                        rule: Rule,
                        properties: propertiesBuilder.ToImmutable(),
                        args: containingType));
            }
        }

        private static bool IsLeftCountComparison(IBinaryOperation binaryOperation, [NotNullWhen(true)] out ITypeSymbol? containingType, out bool shouldNegate)
        {
            containingType = default;
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

            return IsCountPropertyReferenced(binaryOperation.LeftOperand, out containingType);
        }

        private static bool IsRightCountComparison(IBinaryOperation binaryOperation, [NotNullWhen(true)] out ITypeSymbol? containingType, out bool shouldNegate)
        {
            containingType = default;
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

            return IsCountPropertyReferenced(binaryOperation.RightOperand, out containingType);
        }

        private static bool IsCountPropertyReferenced(IOperation operation, [NotNullWhen(true)] out ITypeSymbol? containingType)
        {
            operation = operation.WalkDownParentheses();

            if (operation is IPropertyReferenceOperation propertyOperation && propertyOperation.Property.Name == Count)
            {
                containingType = propertyOperation.Property.ContainingType;
                return true;
            }

            containingType = default;
            return false;
        }

        private static bool TypeContainsValidIsEmptyProperty(ITypeSymbol type, OperationAnalysisContext context)
        {
            if (type.GetMembers(IsEmpty).FirstOrDefault() is IPropertySymbol property)
            {
                return
                    !property.IsStatic &&
                    property.Type.SpecialType == SpecialType.System_Boolean &&
                    property.GetMethod != null &&
                    context.Compilation.IsSymbolAccessibleWithin(property, context.ContainingSymbol.ContainingType) &&
                    context.Compilation.IsSymbolAccessibleWithin(property.GetMethod, context.ContainingSymbol.ContainingType);
            }

            return false;
        }

        private static bool TryGetZeroOrOneConstant(IOperation operation, out int constant)
        {
            constant = default;

            if (operation?.Type?.SpecialType != SpecialType.System_Int32 &&
                operation?.Type?.SpecialType != SpecialType.System_Int64 &&
                operation?.Type?.SpecialType != SpecialType.System_UInt32 &&
                operation?.Type?.SpecialType != SpecialType.System_UInt64 &&
                operation?.Type?.SpecialType != SpecialType.System_Object)
            {
                return false;
            }

            var comparandValueOpt = operation.ConstantValue;

            if (!comparandValueOpt.HasValue)
            {
                return false;
            }

            switch (comparandValueOpt.Value)
            {
                case int intValue:
                    constant = intValue;
                    break;
                case uint uintValue:
                    constant = (int)uintValue;
                    break;
                case long longValue:
                    constant = (int)longValue;
                    break;
                case ulong ulongValue:
                    constant = (int)ulongValue;
                    break;
                default:
                    return false;
            }

            return constant == 0 || constant == 1;
        }
    }
}
