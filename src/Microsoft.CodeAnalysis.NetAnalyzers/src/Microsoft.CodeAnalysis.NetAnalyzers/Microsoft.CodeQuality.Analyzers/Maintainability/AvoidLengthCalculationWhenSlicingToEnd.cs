// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1514: <inheritdoc cref="AvoidLengthCalculationWhenSlicingToEndTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidLengthCalculationWhenSlicingToEndAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1514";

        private const string Substring = nameof(Substring);

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AvoidLengthCalculationWhenSlicingToEndTitle)),
            CreateLocalizableResourceString(nameof(AvoidLengthCalculationWhenSlicingToEndMessage)),
            DiagnosticCategory.Maintainability,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(AvoidLengthCalculationWhenSlicingToEndDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!RequiredSymbols.TryGetSymbols(context.Compilation, out var symbols))
            {
                return;
            }

            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

            void AnalyzeInvocation(OperationAnalysisContext context)
            {
                var invocation = (IInvocationOperation)context.Operation;

                if (!symbols.IsAnyStartLengthMethod(invocation.TargetMethod))
                {
                    return;
                }

                var instance = invocation.Instance;
                var argumentsInParameterOrder = invocation.Arguments.GetArgumentsInParameterOrder();
                var startArgument = argumentsInParameterOrder[0];
                var lengthArgument = argumentsInParameterOrder[1];

                if (OperationHasSideEffects(instance) ||
                    ArgumentHasSideEffects(startArgument) ||
                    !symbols.HasLengthPropertyOnInstance(lengthArgument, instance, out var lengthProperty) ||
                    !StartIsSubtractedFromLength(startArgument, lengthArgument, lengthProperty))
                {
                    return;
                }

                context.ReportDiagnostic(invocation.CreateDiagnostic(Rule, invocation.TargetMethod.ToDisplayString()));
            }

            // Fields, locals, parameters and instance references have no side effects.
            // Properties have no side effects if they are auto-properties.
            // For unary, binary and parenthesized operations it depends on the operand and whether the operator is supported.
            bool OperationHasSideEffects(IOperation? operation)
            {
                return operation switch
                {
                    IFieldReferenceOperation => false,
                    ILocalReferenceOperation => false,
                    IParameterReferenceOperation => false,
                    ILiteralOperation => false,
                    IInstanceReferenceOperation => false,
                    IPropertyReferenceOperation propertyReferenceOperation => !propertyReferenceOperation.Property.IsAutoProperty(),
                    IUnaryOperation { OperatorKind: UnaryOperatorKind.Plus or UnaryOperatorKind.Minus } unaryOperation
                        => OperationHasSideEffects(unaryOperation.Operand),
                    IBinaryOperation { OperatorKind: BinaryOperatorKind.Add or BinaryOperatorKind.Subtract } binaryOperation
                        => OperationHasSideEffects(binaryOperation.LeftOperand) || OperationHasSideEffects(binaryOperation.RightOperand),
                    IParenthesizedOperation parenthesizedOperation => OperationHasSideEffects(parenthesizedOperation.Operand),
                    _ => true
                };
            }

            bool ArgumentHasSideEffects(IArgumentOperation argument)
            {
                return argument
                    .Descendants()
                    .OfType<IOperation>()
                    .Any(OperationHasSideEffects);
            }

            bool StartIsSubtractedFromLength(IArgumentOperation startArgument, IArgumentOperation lengthArgument, IPropertyReferenceOperation lengthProperty)
            {
                // Keep track of constants: Add constants from start argument and subtract constants from the length argument.
                // The first condition that the start argument is subtracted from the length argument is that the constant sum is zero.
                int constantSum = 0;

                // Build two sets containing all symbols in the expressions that are part of the start and length argument.
                // The second condition that the start argument is subtracted from the length argument is that the symbol sets are equal.
                // For this reason, the symbols of the length argument start negated.

                var startArgumentSymbols = new HashSet<SymbolWithNegation>();
                int constantsSign = 1;
                AddExpressionPartToSet(startArgument.Value, isNegated: false, startArgumentSymbols);

                var lengthArgumentSymbols = new HashSet<SymbolWithNegation>();
                constantsSign = -1;
                AddExpressionPartToSet(lengthArgument.Value, isNegated: true, lengthArgumentSymbols);

                // Remove length property which is not present in start argument.
                lengthArgumentSymbols.Remove(new SymbolWithNegation(lengthProperty.Member, true));

                return startArgumentSymbols.SetEquals(lengthArgumentSymbols) && constantSum == 0;

                void AddExpressionPartToSet(IOperation operation, bool isNegated, HashSet<SymbolWithNegation> set)
                {
                    if (operation is IBinaryOperation binaryOperation)
                    {
                        AddExpressionPartToSet(binaryOperation.LeftOperand, isNegated, set);
                        AddExpressionPartToSet(binaryOperation.RightOperand, binaryOperation.OperatorKind == BinaryOperatorKind.Subtract ? !isNegated : isNegated, set);
                    }
                    else if (operation is IUnaryOperation unaryOperation)
                    {
                        AddExpressionPartToSet(unaryOperation.Operand, unaryOperation.OperatorKind == UnaryOperatorKind.Minus ? !isNegated : isNegated, set);
                    }
                    else if (operation is IParenthesizedOperation parenthesizedOperation)
                    {
                        AddExpressionPartToSet(parenthesizedOperation.Operand, isNegated, set);
                    }
                    else if (operation is ILiteralOperation { ConstantValue.Value: int } literalOperation)
                    {
                        constantSum += (int)literalOperation.ConstantValue.Value! * (isNegated ? -1 : 1) * constantsSign;
                    }
                    else
                    {
                        set.Add(new SymbolWithNegation(operation.GetReferencedMemberOrLocalOrParameter(), isNegated));
                    }
                }
            }
        }

        private readonly record struct SymbolWithNegation(ISymbol? Symbol, bool IsNegated);

        private sealed class RequiredSymbols
        {
            private RequiredSymbols(
                IMethodSymbol? substringStartLength,
                IMethodSymbol? spanSliceStartLength,
                IMethodSymbol? readOnlySpanSliceStartLength,
                IMethodSymbol? memorySliceStartLength,
                IPropertySymbol? stringLength,
                IPropertySymbol? spanLength,
                IPropertySymbol? readOnlySpanLength,
                IPropertySymbol? memoryLength)
            {
                SubstringStartLength = substringStartLength;
                SpanSliceStartLength = spanSliceStartLength;
                ReadOnlySpanSliceStartLength = readOnlySpanSliceStartLength;
                MemorySliceStartLength = memorySliceStartLength;
                StringLength = stringLength;
                SpanLength = spanLength;
                ReadOnlySpanLength = readOnlySpanLength;
                MemoryLength = memoryLength;
            }

            public static bool TryGetSymbols(Compilation compilation, [NotNullWhen(true)] out RequiredSymbols? symbols)
            {
                symbols = default;

                var int32Type = compilation.GetSpecialType(SpecialType.System_Int32);
                var stringType = compilation.GetSpecialType(SpecialType.System_String);
                var spanType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSpan1);
                var readOnlySpanType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1);
                var memoryType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMemory1);

                // Bail out if we have no integer type or all target types are null
                if (int32Type is null ||
                    (stringType is null && spanType is null && readOnlySpanType is null && memoryType is null))
                {
                    return false;
                }

                var int32ParamInfo = ParameterInfo.GetParameterInfo(int32Type);

                var substringMembers = stringType?.GetMembers(Substring).OfType<IMethodSymbol>();
                var substringStartLength = substringMembers.GetFirstOrDefaultMemberWithParameterInfos(int32ParamInfo, int32ParamInfo);
                var stringLength = stringType?.GetMembers(WellKnownMemberNames.LengthPropertyName).OfType<IPropertySymbol>().FirstOrDefault();

                var spanSliceMembers = spanType?.GetMembers(WellKnownMemberNames.SliceMethodName).OfType<IMethodSymbol>();
                var spanSliceStartLength = spanSliceMembers.GetFirstOrDefaultMemberWithParameterInfos(int32ParamInfo, int32ParamInfo);
                var spanLength = spanType?.GetMembers(WellKnownMemberNames.LengthPropertyName).OfType<IPropertySymbol>().FirstOrDefault();

                var readOnlySpanSliceMembers = readOnlySpanType?.GetMembers(WellKnownMemberNames.SliceMethodName).OfType<IMethodSymbol>();
                var readOnlySpanSliceStartLength = readOnlySpanSliceMembers.GetFirstOrDefaultMemberWithParameterInfos(int32ParamInfo, int32ParamInfo);
                var readOnlySpanLength = readOnlySpanType?.GetMembers(WellKnownMemberNames.LengthPropertyName).OfType<IPropertySymbol>().FirstOrDefault();

                var memorySliceMembers = memoryType?.GetMembers(WellKnownMemberNames.SliceMethodName).OfType<IMethodSymbol>();
                var memorySliceStartLength = memorySliceMembers.GetFirstOrDefaultMemberWithParameterInfos(int32ParamInfo, int32ParamInfo);
                var memoryLength = memoryType?.GetMembers(WellKnownMemberNames.LengthPropertyName).OfType<IPropertySymbol>().FirstOrDefault();

                // Bail out if we have no complete method pair
                if ((substringStartLength is null || stringLength is null) &&
                    (spanSliceStartLength is null || spanLength is null) &&
                    (readOnlySpanSliceStartLength is null || readOnlySpanLength is null) &&
                    (memorySliceStartLength is null || memoryLength is null))
                {
                    return false;
                }

                symbols = new RequiredSymbols(
                    substringStartLength, spanSliceStartLength, readOnlySpanSliceStartLength, memorySliceStartLength,
                    stringLength, spanLength, readOnlySpanLength, memoryLength);

                return true;
            }

            public IMethodSymbol? SubstringStartLength { get; }
            public IMethodSymbol? SpanSliceStartLength { get; }
            public IMethodSymbol? ReadOnlySpanSliceStartLength { get; }
            public IMethodSymbol? MemorySliceStartLength { get; }
            public IPropertySymbol? StringLength { get; }
            public IPropertySymbol? SpanLength { get; }
            public IPropertySymbol? ReadOnlySpanLength { get; }
            public IPropertySymbol? MemoryLength { get; }

            public bool IsAnyStartLengthMethod(IMethodSymbol method)
            {
                return SymbolEqualityComparer.Default.Equals(method.OriginalDefinition, SubstringStartLength) ||
                    SymbolEqualityComparer.Default.Equals(method.OriginalDefinition, SpanSliceStartLength) ||
                    SymbolEqualityComparer.Default.Equals(method.OriginalDefinition, ReadOnlySpanSliceStartLength) ||
                    SymbolEqualityComparer.Default.Equals(method.OriginalDefinition, MemorySliceStartLength);
            }

            public bool IsAnyLengthProperty(IPropertySymbol property)
            {
                return SymbolEqualityComparer.Default.Equals(property.OriginalDefinition, StringLength) ||
                    SymbolEqualityComparer.Default.Equals(property.OriginalDefinition, SpanLength) ||
                    SymbolEqualityComparer.Default.Equals(property.OriginalDefinition, ReadOnlySpanLength) ||
                    SymbolEqualityComparer.Default.Equals(property.OriginalDefinition, MemoryLength);
            }

            public bool HasLengthPropertyOnInstance(IArgumentOperation argument, IOperation? instance, [NotNullWhen(true)] out IPropertyReferenceOperation? lengthProperty)
            {
                lengthProperty = argument
                    .DescendantsAndSelf()
                    .OfType<IPropertyReferenceOperation>()
                    .Where(p => IsAnyLengthProperty(p.Property))
                    .FirstOrDefault();

                if (lengthProperty is null)
                {
                    return false;
                }

                return AreSameInstance(lengthProperty.Instance, instance);
            }
        }

        private static bool AreSameInstance(IOperation? instance1, IOperation? instance2)
        {
            return (instance1, instance2) switch
            {
                (IFieldReferenceOperation fieldRef1, IFieldReferenceOperation fieldRef2) => fieldRef1.Member == fieldRef2.Member,
                (IPropertyReferenceOperation propRef1, IPropertyReferenceOperation propRef2) => propRef1.Member == propRef2.Member,
                (IParameterReferenceOperation paramRef1, IParameterReferenceOperation paramRef2) => paramRef1.Parameter == paramRef2.Parameter,
                (ILocalReferenceOperation localRef1, ILocalReferenceOperation localRef2) => localRef1.Local == localRef2.Local,
                (ILiteralOperation literalRef1, ILiteralOperation literalRef2) => literalRef1.ConstantValue.HasValue && literalRef1.ConstantValue.Value!.Equals(literalRef2.ConstantValue.Value),
                _ => false,
            };
        }
    }
}
