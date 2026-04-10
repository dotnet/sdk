// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;
    /// <summary>
    /// CA2020: <inheritdoc cref="PreventNumericIntPtrUIntPtrBehavioralChangesTitle"/>
    /// Detects Behavioral Changes introduced by new Numeric IntPtr UIntPtr feature
    /// </summary>
    public abstract class PreventNumericIntPtrUIntPtrBehavioralChanges : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2020";
        private static readonly LocalizableResourceString s_titleResource = CreateLocalizableResourceString(nameof(PreventNumericIntPtrUIntPtrBehavioralChangesTitle));
        private static readonly LocalizableResourceString s_descriptionResource = CreateLocalizableResourceString(nameof(PreventNumericIntPtrUIntPtrBehavioralChangesDescription));

        internal static readonly DiagnosticDescriptor OperatorThrowsRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_titleResource,
            CreateLocalizableResourceString(nameof(PreventNumericIntPtrUIntPtrBehavioralChangesOperatorThrowsMessage)),
            DiagnosticCategory.Reliability,
            RuleLevel.IdeSuggestion,
            s_descriptionResource,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor ConversionThrowsRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_titleResource,
            CreateLocalizableResourceString(nameof(PreventNumericIntPtrUIntPtrBehavioralChangesConversionThrowsMessage)),
            DiagnosticCategory.Reliability,
            RuleLevel.IdeSuggestion,
            s_descriptionResource,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor ConversionNotThrowRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_titleResource,
            CreateLocalizableResourceString(nameof(PreventNumericIntPtrUIntPtrBehavioralChangesConversionNotThrowMessage)),
            DiagnosticCategory.Reliability,
            RuleLevel.IdeSuggestion,
            s_descriptionResource,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(OperatorThrowsRule, ConversionThrowsRule, ConversionNotThrowRule);

        protected abstract bool IsWithinCheckedContext(IOperation operation);

        protected abstract bool IsAliasUsed(ISymbol? symbol);

        protected abstract bool IsAliasUsed(SyntaxNode syntax);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesRuntimeFeature, out var runtimeFeatureType) ||
                    !runtimeFeatureType.GetMembers("NumericIntPtr").OfType<IFieldSymbol>().Any())
                {
                    // Numeric IntPtr feature not available
                    return;
                }

                context.RegisterOperationAction(context =>
                {
                    if (context.Operation is IBinaryOperation binaryOperation &&
                        binaryOperation.IsAdditionOrSubstractionOperation(out var binaryOperator) &&
                        binaryOperation.IsChecked)
                    {
                        if ((binaryOperation.LeftOperand.Type?.SpecialType == SpecialType.System_IntPtr ||
                             binaryOperation.LeftOperand.Type?.SpecialType == SpecialType.System_UIntPtr) &&
                             IsConversionFromInt32(binaryOperation.RightOperand) &&
                             !IsAliasUsed(GetSymbol(binaryOperation.LeftOperand)))
                        {
                            context.ReportDiagnostic(binaryOperation.CreateDiagnostic(OperatorThrowsRule, binaryOperator));
                        }
                    }
                    else if (context.Operation is IConversionOperation conversionOperation)
                    {
                        var operation = conversionOperation.WalkDownConversion(c => c.IsImplicit); // get innermost conversion
                        if (operation is IConversionOperation explicitConversion &&
                            explicitConversion.OperatorMethod == null) // Built in conversion
                        {
                            if (explicitConversion.IsChecked ||
                                IsWithinCheckedContext(explicitConversion))
                            {
                                if (IsIntPtrToOrFromPtrConversion(explicitConversion.Type, explicitConversion.Operand.Type) &&
                                    !IsAliasUsed(GetSymbol(explicitConversion.Operand)))
                                {
                                    context.ReportDiagnostic(explicitConversion.CreateDiagnostic(ConversionThrowsRule,
                                        PopulateConversionString(explicitConversion.Type, explicitConversion.Operand.Type)));
                                }
                                else if (IsIntPtrToOrFromPtrConversion(explicitConversion.Operand.Type, explicitConversion.Type) &&
                                         !IsAliasUsed(explicitConversion.Syntax))
                                {
                                    context.ReportDiagnostic(explicitConversion.CreateDiagnostic(ConversionThrowsRule,
                                        PopulateConversionString(explicitConversion.Type, explicitConversion.Operand.Type)));
                                }
                            }
                            else // unchecked context
                            {
                                if ((IsLongToIntPtrConversion(explicitConversion.Type, explicitConversion.Operand.Type) ||
                                     IsULongToUIntPtrConversion(explicitConversion.Type, explicitConversion.Operand.Type)) &&
                                    !IsAliasUsed(explicitConversion.Syntax))
                                {
                                    context.ReportDiagnostic(explicitConversion.CreateDiagnostic(ConversionNotThrowRule,
                                        PopulateConversionString(explicitConversion.Type, explicitConversion.Operand.Type)));
                                }
                                else if ((IsIntPtrToIntConversion(explicitConversion.Type, explicitConversion.Operand.Type) ||
                                          IsUIntPtrToUIntConversion(explicitConversion.Type, explicitConversion.Operand.Type)) &&
                                        !IsAliasUsed(GetSymbol(explicitConversion.Operand)))
                                {
                                    context.ReportDiagnostic(explicitConversion.CreateDiagnostic(ConversionNotThrowRule,
                                        PopulateConversionString(explicitConversion.Type, explicitConversion.Operand.Type)));
                                }
                            }
                        }
                    }
                },
                OperationKind.Binary, OperationKind.Conversion);
            });

            static string PopulateConversionString(ITypeSymbol type, ITypeSymbol operand)
            {
                string typeName = type.Name;
                string operandName = operand.Name;

                if (type is IPointerTypeSymbol)
                {
                    typeName = type.ToString();
                }

                if (operand is IPointerTypeSymbol)
                {
                    operandName = operand.ToString();
                }

                return $"({typeName}){operandName}";
            }

            static ISymbol? GetSymbol(IOperation operation) =>
                operation switch
                {
                    IFieldReferenceOperation fieldReference => fieldReference.Field,
                    IParameterReferenceOperation parameter => parameter.Parameter,
                    ILocalReferenceOperation local => local.Local,
                    _ => null,
                };

            static bool IsConversionFromInt32(IOperation operation) =>
                operation is IConversionOperation conversion &&
                conversion.Operand.Type?.SpecialType == SpecialType.System_Int32;

            static bool IsIntPtrToOrFromPtrConversion([NotNullWhen(true)] ITypeSymbol? pointerType, [NotNullWhen(true)] ITypeSymbol? intPtrType) =>
                intPtrType?.SpecialType == SpecialType.System_IntPtr &&
                pointerType is IPointerTypeSymbol pointer;

            static bool IsLongToIntPtrConversion([NotNullWhen(true)] ITypeSymbol? convertingType, [NotNullWhen(true)] ITypeSymbol? operandType) =>
                convertingType?.SpecialType == SpecialType.System_IntPtr &&
                operandType?.SpecialType == SpecialType.System_Int64;

            static bool IsIntPtrToIntConversion([NotNullWhen(true)] ITypeSymbol? convertingType, [NotNullWhen(true)] ITypeSymbol? operandType) =>
                convertingType?.SpecialType == SpecialType.System_Int32 &&
                operandType?.SpecialType == SpecialType.System_IntPtr;

            static bool IsULongToUIntPtrConversion([NotNullWhen(true)] ITypeSymbol? convertingType, [NotNullWhen(true)] ITypeSymbol? operandType) =>
                convertingType?.SpecialType == SpecialType.System_UIntPtr &&
                operandType?.SpecialType == SpecialType.System_UInt64;

            static bool IsUIntPtrToUIntConversion([NotNullWhen(true)] ITypeSymbol? convertingType, [NotNullWhen(true)] ITypeSymbol? operandType) =>
                convertingType?.SpecialType == SpecialType.System_UInt32 &&
                operandType?.SpecialType == SpecialType.System_UIntPtr;
        }
    }
}

