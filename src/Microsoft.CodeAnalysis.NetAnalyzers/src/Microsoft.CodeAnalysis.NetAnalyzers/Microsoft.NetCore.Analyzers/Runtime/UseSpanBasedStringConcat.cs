// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class UseSpanBasedStringConcat : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1845";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(Resx.UseSpanBasedStringConcatTitle), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(Resx.UseSpanBasedStringConcatMessage), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(Resx.UseSpanBasedStringConcatDescription), Resx.ResourceManager, typeof(Resx));

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        /// <summary>
        /// If the specified binary operation is a string concatenation operation, we try to walk up to the top-most
        /// string-concatenation operation that it is part of. If it is not a string-concatenation operation, we simply
        /// return false.
        /// </summary>
        private protected abstract bool TryGetTopMostConcatOperation(IBinaryOperation binaryOperation, [NotNullWhen(true)] out IBinaryOperation? rootBinaryOperation);

        /// <summary>
        /// Remove the built in implicit conversion on operands to concat.
        /// In VB, the conversion can be to either string or object.
        /// In C#, the conversion is always to object.
        /// </summary>
        private protected abstract IOperation WalkDownBuiltInImplicitConversionOnConcatOperand(IOperation operand);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!RequiredSymbols.TryGetSymbols(context.Compilation, out RequiredSymbols symbols))
                return;

            context.RegisterOperationBlockStartAction(OnOperationBlockStart);
            return;

            // Local functions
            void OnOperationBlockStart(OperationBlockStartAnalysisContext context)
            {
                //  Maintain set of all top-most concat operations so we don't report sub-expressions of an
                //  already-reported violation. 
                //  We also don't report any diagnostic if the concat operation has too many operands for the span-based
                //  Concat overloads to handle.
                var topMostConcatOperations = PooledConcurrentSet<IBinaryOperation>.GetInstance();

                context.RegisterOperationAction(PopulateTopMostConcatOperations, OperationKind.Binary);
                context.RegisterOperationBlockEndAction(ReportDiagnosticsOnRootConcatOperationsWithSubstringCalls);

                void PopulateTopMostConcatOperations(OperationAnalysisContext context)
                {
                    //  If the current operation is a string-concatenation operation, walk up to the top-most concat
                    //  operation and add it to the set.
                    var binary = (IBinaryOperation)context.Operation;
                    if (!TryGetTopMostConcatOperation(binary, out var topMostConcatOperation))
                        return;

                    topMostConcatOperations.Add(topMostConcatOperation);
                }

                void ReportDiagnosticsOnRootConcatOperationsWithSubstringCalls(OperationBlockAnalysisContext context)
                {
                    //  We report diagnostics for all top-most concat operations that contain 
                    //  direct or conditional substring invocations when there is an applicable span-based overload of
                    //  the string.Concat method.
                    //  We don't report when the concatenation contains anything other than strings or character literals.
                    foreach (var operation in topMostConcatOperations)
                    {
                        if (ShouldBeReported(operation))
                        {
                            context.ReportDiagnostic(operation.CreateDiagnostic(Rule));
                        }
                    }

                    topMostConcatOperations.Free(context.CancellationToken);
                }
            }

            bool ShouldBeReported(IBinaryOperation topMostConcatOperation)
            {
                var concatOperands = FlattenBinaryOperation(topMostConcatOperation);

                //  Bail if no suitable overload of 'string.Concat' exists.
                if (!symbols.TryGetRoscharConcatMethodWithArity(concatOperands.Length, out _))
                    return false;

                bool anySubstringInvocations = false;
                foreach (var operand in concatOperands)
                {
                    var value = WalkDownBuiltInImplicitConversionOnConcatOperand(operand);
                    switch (value.Type?.SpecialType)
                    {
                        //  Report diagnostics only when operands are exclusively strings and character literals.
                        case SpecialType.System_String:
                        case SpecialType.System_Char when value is ILiteralOperation:
                            if (IsAnyDirectOrConditionalSubstringInvocation(value))
                                anySubstringInvocations = true;
                            break;
                        default:
                            return false;
                    }
                }

                return anySubstringInvocations;
            }

            bool IsAnyDirectOrConditionalSubstringInvocation(IOperation operation)
            {
                if (operation is IConditionalAccessOperation conditionallAccessOperation)
                    operation = conditionallAccessOperation.WhenNotNull;

                return operation is IInvocationOperation invocation && symbols.IsAnySubstringMethod(invocation.TargetMethod);
            }
        }

        internal static ImmutableArray<IOperation> FlattenBinaryOperation(IBinaryOperation root)
        {
            var walker = new BinaryOperandWalker();
            walker.Visit(root);

            return walker.Operands.ToImmutable();
        }

        private sealed class BinaryOperandWalker : OperationWalker
        {
            private BinaryOperatorKind _operatorKind;

            public BinaryOperandWalker() : base() { }

            public ImmutableArray<IOperation>.Builder Operands { get; } = ImmutableArray.CreateBuilder<IOperation>();

            public override void DefaultVisit(IOperation operation)
            {
                Operands.Add(operation);
            }

            public override void VisitBinaryOperator(IBinaryOperation operation)
            {
                if (_operatorKind is BinaryOperatorKind.None)
                {
                    _operatorKind = operation.OperatorKind;
                }
                else if (_operatorKind != operation.OperatorKind)
                {
                    DefaultVisit(operation);
                    return;
                }

                Visit(operation.LeftOperand);
                Visit(operation.RightOperand);
            }
        }

        internal static IOperation CSharpWalkDownBuiltInImplicitConversionOnConcatOperand(IOperation operand)
        {
            if (operand is not IConversionOperation conversion)
                return operand;
            if (!conversion.IsImplicit || conversion.Conversion.IsUserDefined)
                return conversion;
            if (conversion.Type.SpecialType is SpecialType.System_Object)
                return conversion.Operand;

            return conversion;
        }

        internal static IOperation BasicWalkDownBuiltInImplicitConversionOnConcatOperand(IOperation operand)
        {
            if (operand is not IConversionOperation conversion)
                return operand;
            if (!conversion.IsImplicit || conversion.Conversion.IsUserDefined)
                return conversion;
            if (conversion.Type.SpecialType is SpecialType.System_Object or SpecialType.System_String)
                return conversion.Operand;

            return conversion;
        }

        // Use readonly struct instead of record type to save on allocations, since it's not passed by-value.
        // We aren't comparing these.
#pragma warning disable CA1815 // Override equals and operator equals on value types
        internal readonly struct RequiredSymbols
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            private RequiredSymbols(
                INamedTypeSymbol stringType, INamedTypeSymbol roscharType,
                IMethodSymbol substringStart, IMethodSymbol substringStartLength,
                IMethodSymbol asSpanStart, IMethodSymbol asSpanStartLength)
            {
                StringType = stringType;
                ReadOnlySpanOfCharType = roscharType;
                SubstringStart = substringStart;
                SubstringStartLength = substringStartLength;
                AsSpanStart = asSpanStart;
                AsSpanStartLength = asSpanStartLength;
            }

            public static bool TryGetSymbols(Compilation compilation, out RequiredSymbols symbols)
            {
                var stringType = compilation.GetSpecialType(SpecialType.System_String);
                var charType = compilation.GetSpecialType(SpecialType.System_Char);

                if (stringType is null || charType is null)
                {
                    symbols = default;
                    return false;
                }

                var readOnlySpanOfCharType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1)?.Construct(charType);
                var memoryExtensionsType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMemoryExtensions);

                if (readOnlySpanOfCharType is null || memoryExtensionsType is null)
                {
                    symbols = default;
                    return false;
                }

                var intParamInfo = ParameterInfo.GetParameterInfo(compilation.GetSpecialType(SpecialType.System_Int32));
                var stringParamInfo = ParameterInfo.GetParameterInfo(stringType);

                var substringMembers = stringType.GetMembers(nameof(string.Substring)).OfType<IMethodSymbol>();
                var substringStart = substringMembers.GetFirstOrDefaultMemberWithParameterInfos(intParamInfo);
                var substringStartLength = substringMembers.GetFirstOrDefaultMemberWithParameterInfos(intParamInfo, intParamInfo);

                var asSpanMembers = memoryExtensionsType.GetMembers(nameof(MemoryExtensions.AsSpan)).OfType<IMethodSymbol>();
                var asSpanStart = asSpanMembers.GetFirstOrDefaultMemberWithParameterInfos(stringParamInfo, intParamInfo)?.ReduceExtensionMethod(stringType);
                var asSpanStartLength = asSpanMembers.GetFirstOrDefaultMemberWithParameterInfos(stringParamInfo, intParamInfo, intParamInfo)?.ReduceExtensionMethod(stringType);

                if (substringStart is null || substringStartLength is null || asSpanStart is null || asSpanStartLength is null)
                {
                    symbols = default;
                    return false;
                }

                symbols = new RequiredSymbols(
                    stringType, readOnlySpanOfCharType,
                    substringStart, substringStartLength,
                    asSpanStart, asSpanStartLength);
                return true;
            }

            public INamedTypeSymbol StringType { get; }
            public INamedTypeSymbol ReadOnlySpanOfCharType { get; }
            public IMethodSymbol SubstringStart { get; }
            public IMethodSymbol SubstringStartLength { get; }
            public IMethodSymbol AsSpanStart { get; }
            public IMethodSymbol AsSpanStartLength { get; }

            public IMethodSymbol? GetAsSpanEquivalent(IMethodSymbol? substringMethod)
            {
                if (SymbolEqualityComparer.Default.Equals(substringMethod, SubstringStart))
                    return AsSpanStart;
                if (SymbolEqualityComparer.Default.Equals(substringMethod, SubstringStartLength))
                    return AsSpanStartLength;
                return null;
            }

            public bool IsAnySubstringMethod(IMethodSymbol? method)
            {
                return SymbolEqualityComparer.Default.Equals(method, SubstringStart) ||
                    SymbolEqualityComparer.Default.Equals(method, SubstringStartLength);
            }

            public bool IsAnySubstringStartIndexParameter(IParameterSymbol? parameter)
            {
                return SymbolEqualityComparer.Default.Equals(parameter, SubstringStart.Parameters.First()) ||
                    SymbolEqualityComparer.Default.Equals(parameter, SubstringStartLength.Parameters.First());
            }

            public bool TryGetRoscharConcatMethodWithArity(int arity, [NotNullWhen(true)] out IMethodSymbol? concatMethod)
            {
                var roscharParamInfo = ParameterInfo.GetParameterInfo(ReadOnlySpanOfCharType);
                var argumentList = new ParameterInfo[arity];
                for (int index = 0; index < arity; index++)
                    argumentList[index] = roscharParamInfo;

                concatMethod = StringType.GetMembers(nameof(string.Concat))
                    .OfType<IMethodSymbol>()
                    .GetFirstOrDefaultMemberWithParameterInfos(argumentList);
                return concatMethod is not null;
            }
        }
    }
}
