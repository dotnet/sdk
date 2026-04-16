// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    /// CA1516: <inheritdoc cref="UseCrossPlatformIntrinsicsTitle"/>
    /// </summary>
    public abstract partial class UseCrossPlatformIntrinsicsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1516";

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(UseCrossPlatformIntrinsicsTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(UseCrossPlatformIntrinsicsDescription));

        internal static readonly ImmutableArray<DiagnosticDescriptor> Rules = ImmutableArray.CreateRange(
            Enumerable.Range(0, (int)RuleKind.Count)
                      .Select(i => CreateDiagnosticDescriptor((RuleKind)i))
        );

        internal static readonly ImmutableArray<ImmutableDictionary<string, string?>> Properties = ImmutableArray.CreateRange(
            Enumerable.Range(0, (int)RuleKind.Count)
                      .Select(i =>
                      {
                          ImmutableDictionary<string, string?>.Builder builder = ImmutableDictionary.CreateBuilder<string, string?>();
                          builder[nameof(RuleKind)] = ((RuleKind)i).ToString();
                          return builder.ToImmutable();
                      })
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Rules;

        private static DiagnosticDescriptor CreateDiagnosticDescriptor(RuleKind ruleKind) => DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString($"UseCrossPlatformIntrinsicsMessage_{ruleKind}"),
            DiagnosticCategory.Maintainability,
            RuleLevel.IdeSuggestion,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false
        );

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        protected virtual bool IsSupported(IInvocationOperation invocation, RuleKind ruleKind)
        {
            // We need to validate that the invocation is the expected syntax kind and
            // that the diagnostic is valid to report for its shape. This includes ensuring
            // that the right number of arguments and their types are correct, since we
            // may have bound an invocation for code that has a separate error diagnostic.

            return ruleKind switch
            {
                RuleKind.op_Addition or
                RuleKind.op_BitwiseAnd or
                RuleKind.op_BitwiseOr or
                RuleKind.op_ExclusiveOr or
                RuleKind.op_Multiply or
                RuleKind.op_Subtraction => IsValidBinaryOperatorMethodInvocation(invocation, isCommutative: true),

                RuleKind.op_Division => IsValidBinaryOperatorMethodInvocation(invocation, isCommutative: false),

                RuleKind.op_LeftShift or
                RuleKind.op_RightShift or
                RuleKind.op_UnsignedRightShift => IsValidShiftOperatorMethodInvocation(invocation),

                RuleKind.op_OnesComplement or
                RuleKind.op_UnaryNegation => IsValidUnaryOperatorMethodInvocation(invocation),

                _ => false,
            };

            static bool IsValidBinaryOperatorMethodInvocation(IInvocationOperation invocation, bool isCommutative)
            {
                return (invocation.Arguments.Length == 2) &&
                       SymbolEqualityComparer.Default.Equals(invocation.Type, invocation.Arguments[0].Parameter?.Type) &&
                       SymbolEqualityComparer.Default.Equals(invocation.Type, invocation.Arguments[1].Parameter?.Type) &&
                       (isCommutative || (invocation.Arguments[0].Parameter?.Ordinal == 0));
            }

            static bool IsValidShiftOperatorMethodInvocation(IInvocationOperation invocation)
            {
                return (invocation.Arguments.Length == 2) &&
                       SymbolEqualityComparer.Default.Equals(invocation.Type, invocation.Arguments[0].Parameter?.Type) &&
                       (invocation.Arguments[1].Parameter?.Type.SpecialType is SpecialType.System_Byte or SpecialType.System_Int32) &&
                       (invocation.Arguments[0].Parameter?.Ordinal == 0);
            }

            static bool IsValidUnaryOperatorMethodInvocation(IInvocationOperation invocation)
            {
                return (invocation.Arguments.Length == 1) &&
                       SymbolEqualityComparer.Default.Equals(invocation.Type, invocation.Arguments[0].Parameter?.Type);
            }
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var compilation = context.Compilation;

            if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsVector64, out var _) ||
                !compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsVector128, out var _) ||
                !compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsVector256, out var _) ||
                !compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsVector512, out var _))
            {
                // The core vector types are not available in the compilation, so we cannot register any operators.
                // This may exclude out of support versions of .NET, such as .NET 6, which only have some of the vector types
                //
                // Notably, this is still not an exact check. There may be custom runtimes or edge case scenarios where a given
                // operator is not available on a given type but the platform specific API is available. In such a case, we will
                // report a diagnostic and the fixer will be reported. If the user applies the fixer, the code would produce an
                // error. This is considered an acceptable tradeoff given there would need to be hundreds of checks to exactly
                // cover the potential scenarios, which would make the analyzer too complex and slow. There will be no diagnostic
                // or fixer reported for in support versions of .NET, such as .NET Standard and .NET Framework; and the diagnostic
                // and fixer reported for .NET 8+ will be correct.
                return;
            }

            // We need to find the platform specific intrinsics that we support replacing with the cross-platform intrinsics. To do
            // this, we need to find the methods under each class by name and signature. In most cases, the methods support "all"
            // types, but in some cases they do not and so we will pass the exact types that we support.

            Dictionary<IMethodSymbol, RuleKind> methodSymbols = new Dictionary<IMethodSymbol, RuleKind>(SymbolEqualityComparer.Default);

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsArmAdvSimd, out var armAdvSimdTypeSymbol))
            {
                AddBinaryOperatorMethods(methodSymbols, "Add", armAdvSimdTypeSymbol, RuleKind.op_Addition);
                AddBinaryOperatorMethods(methodSymbols, "AddScalar", armAdvSimdTypeSymbol, RuleKind.op_Addition, [SpecialType.System_Int64, SpecialType.System_UInt64, SpecialType.System_Double]);
                AddBinaryOperatorMethods(methodSymbols, "And", armAdvSimdTypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryOperatorMethods(methodSymbols, "DivideScalar", armAdvSimdTypeSymbol, RuleKind.op_Division, [SpecialType.System_Double]);
                AddBinaryOperatorMethods(methodSymbols, "Multiply", armAdvSimdTypeSymbol, RuleKind.op_Multiply);
                AddBinaryOperatorMethods(methodSymbols, "MultiplyScalar", armAdvSimdTypeSymbol, RuleKind.op_Multiply, [SpecialType.System_Double]);
                AddBinaryOperatorMethods(methodSymbols, "Or", armAdvSimdTypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryOperatorMethods(methodSymbols, "Subtract", armAdvSimdTypeSymbol, RuleKind.op_Subtraction);
                AddBinaryOperatorMethods(methodSymbols, "SubtractScalar", armAdvSimdTypeSymbol, RuleKind.op_Subtraction, [SpecialType.System_Int64, SpecialType.System_UInt64, SpecialType.System_Double]);
                AddBinaryOperatorMethods(methodSymbols, "Xor", armAdvSimdTypeSymbol, RuleKind.op_ExclusiveOr);

                AddShiftOperatorMethods(methodSymbols, "ShiftLeftLogical", armAdvSimdTypeSymbol, RuleKind.op_LeftShift);
                AddShiftOperatorMethods(methodSymbols, "ShiftLeftLogicalScalar", armAdvSimdTypeSymbol, RuleKind.op_LeftShift, [SpecialType.System_Int64, SpecialType.System_UInt64]);
                AddShiftOperatorMethods(methodSymbols, "ShiftRightArithmetic", armAdvSimdTypeSymbol, RuleKind.op_RightShift);
                AddShiftOperatorMethods(methodSymbols, "ShiftRightArithmeticScalar", armAdvSimdTypeSymbol, RuleKind.op_RightShift, [SpecialType.System_Int64, SpecialType.System_UInt64]);
                AddShiftOperatorMethods(methodSymbols, "ShiftRightLogical", armAdvSimdTypeSymbol, RuleKind.op_UnsignedRightShift);
                AddShiftOperatorMethods(methodSymbols, "ShiftRightLogicalScalar", armAdvSimdTypeSymbol, RuleKind.op_UnsignedRightShift, [SpecialType.System_Int64, SpecialType.System_UInt64]);

                AddUnaryOperatorMethods(methodSymbols, "Negate", armAdvSimdTypeSymbol, RuleKind.op_UnaryNegation);
                AddUnaryOperatorMethods(methodSymbols, "NegateScalar", armAdvSimdTypeSymbol, RuleKind.op_UnaryNegation, [SpecialType.System_Double]);
                AddUnaryOperatorMethods(methodSymbols, "Not", armAdvSimdTypeSymbol, RuleKind.op_OnesComplement);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsArmAdvSimdArm64, out var armAdvSimdArm64TypeSymbol))
            {
                AddBinaryOperatorMethods(methodSymbols, "Add", armAdvSimdArm64TypeSymbol, RuleKind.op_Addition);
                AddBinaryOperatorMethods(methodSymbols, "Divide", armAdvSimdArm64TypeSymbol, RuleKind.op_Division);
                AddBinaryOperatorMethods(methodSymbols, "Multiply", armAdvSimdArm64TypeSymbol, RuleKind.op_Multiply);
                AddBinaryOperatorMethods(methodSymbols, "Subtract", armAdvSimdArm64TypeSymbol, RuleKind.op_Subtraction);

                AddUnaryOperatorMethods(methodSymbols, "Negate", armAdvSimdArm64TypeSymbol, RuleKind.op_UnaryNegation);
                AddUnaryOperatorMethods(methodSymbols, "NegateScalar", armAdvSimdArm64TypeSymbol, RuleKind.op_UnaryNegation, [SpecialType.System_Int64]);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsWasmPackedSimd, out var wasmPackedSimdTypeSymbol))
            {
                AddBinaryOperatorMethods(methodSymbols, "Add", wasmPackedSimdTypeSymbol, RuleKind.op_Addition);
                AddBinaryOperatorMethods(methodSymbols, "And", wasmPackedSimdTypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryOperatorMethods(methodSymbols, "Divide", wasmPackedSimdTypeSymbol, RuleKind.op_Division);
                AddBinaryOperatorMethods(methodSymbols, "Multiply", wasmPackedSimdTypeSymbol, RuleKind.op_Multiply);
                AddBinaryOperatorMethods(methodSymbols, "Or", wasmPackedSimdTypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryOperatorMethods(methodSymbols, "Subtract", wasmPackedSimdTypeSymbol, RuleKind.op_Subtraction);
                AddBinaryOperatorMethods(methodSymbols, "Xor", wasmPackedSimdTypeSymbol, RuleKind.op_ExclusiveOr);

                AddShiftOperatorMethods(methodSymbols, "ShiftLeft", wasmPackedSimdTypeSymbol, RuleKind.op_LeftShift);
                AddShiftOperatorMethods(methodSymbols, "ShiftRightArithmetic", wasmPackedSimdTypeSymbol, RuleKind.op_RightShift);
                AddShiftOperatorMethods(methodSymbols, "ShiftRightLogical", wasmPackedSimdTypeSymbol, RuleKind.op_UnsignedRightShift);

                AddUnaryOperatorMethods(methodSymbols, "Negate", wasmPackedSimdTypeSymbol, RuleKind.op_UnaryNegation);
                AddUnaryOperatorMethods(methodSymbols, "Not", wasmPackedSimdTypeSymbol, RuleKind.op_OnesComplement);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx, out var x86AvxTypeSymbol))
            {
                AddBinaryOperatorMethods(methodSymbols, "Add", x86AvxTypeSymbol, RuleKind.op_Addition);
                AddBinaryOperatorMethods(methodSymbols, "And", x86AvxTypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryOperatorMethods(methodSymbols, "Divide", x86AvxTypeSymbol, RuleKind.op_Division);
                AddBinaryOperatorMethods(methodSymbols, "Multiply", x86AvxTypeSymbol, RuleKind.op_Multiply);
                AddBinaryOperatorMethods(methodSymbols, "Or", x86AvxTypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryOperatorMethods(methodSymbols, "Subtract", x86AvxTypeSymbol, RuleKind.op_Subtraction);
                AddBinaryOperatorMethods(methodSymbols, "Xor", x86AvxTypeSymbol, RuleKind.op_ExclusiveOr);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx2, out var x86Avx2TypeSymbol))
            {
                AddBinaryOperatorMethods(methodSymbols, "Add", x86Avx2TypeSymbol, RuleKind.op_Addition);
                AddBinaryOperatorMethods(methodSymbols, "And", x86Avx2TypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryOperatorMethods(methodSymbols, "MultiplyLow", x86Avx2TypeSymbol, RuleKind.op_Multiply);
                AddBinaryOperatorMethods(methodSymbols, "Or", x86Avx2TypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryOperatorMethods(methodSymbols, "Subtract", x86Avx2TypeSymbol, RuleKind.op_Subtraction);
                AddBinaryOperatorMethods(methodSymbols, "Xor", x86Avx2TypeSymbol, RuleKind.op_ExclusiveOr);

                AddShiftOperatorMethods(methodSymbols, "ShiftLeftLogical", x86Avx2TypeSymbol, RuleKind.op_LeftShift);
                AddShiftOperatorMethods(methodSymbols, "ShiftRightArithmetic", x86Avx2TypeSymbol, RuleKind.op_RightShift);
                AddShiftOperatorMethods(methodSymbols, "ShiftRightLogical", x86Avx2TypeSymbol, RuleKind.op_UnsignedRightShift);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx512BW, out var x86Avx512BWTypeSymbol))
            {
                AddBinaryOperatorMethods(methodSymbols, "Add", x86Avx512BWTypeSymbol, RuleKind.op_Addition);
                AddBinaryOperatorMethods(methodSymbols, "MultiplyLow", x86Avx512BWTypeSymbol, RuleKind.op_Multiply);
                AddBinaryOperatorMethods(methodSymbols, "Subtract", x86Avx512BWTypeSymbol, RuleKind.op_Subtraction);

                AddShiftOperatorMethods(methodSymbols, "ShiftLeftLogical", x86Avx512BWTypeSymbol, RuleKind.op_LeftShift);
                AddShiftOperatorMethods(methodSymbols, "ShiftRightArithmetic", x86Avx512BWTypeSymbol, RuleKind.op_RightShift);
                AddShiftOperatorMethods(methodSymbols, "ShiftRightLogical", x86Avx512BWTypeSymbol, RuleKind.op_UnsignedRightShift);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx512DQ, out var x86Avx512DQTypeSymbol))
            {
                AddBinaryOperatorMethods(methodSymbols, "And", x86Avx512DQTypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryOperatorMethods(methodSymbols, "MultiplyLow", x86Avx512DQTypeSymbol, RuleKind.op_Multiply);
                AddBinaryOperatorMethods(methodSymbols, "Or", x86Avx512DQTypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryOperatorMethods(methodSymbols, "Xor", x86Avx512DQTypeSymbol, RuleKind.op_ExclusiveOr);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx512DQVL, out var x86Avx512DQVLTypeSymbol))
            {
                AddBinaryOperatorMethods(methodSymbols, "MultiplyLow", x86Avx512DQVLTypeSymbol, RuleKind.op_Multiply);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx512F, out var x86Avx512FTypeSymbol))
            {
                AddBinaryOperatorMethods(methodSymbols, "Add", x86Avx512FTypeSymbol, RuleKind.op_Addition);
                AddBinaryOperatorMethods(methodSymbols, "And", x86Avx512FTypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryOperatorMethods(methodSymbols, "Divide", x86Avx512FTypeSymbol, RuleKind.op_Division);
                AddBinaryOperatorMethods(methodSymbols, "Multiply", x86Avx512FTypeSymbol, RuleKind.op_Multiply, [SpecialType.System_Single, SpecialType.System_Double]);
                AddBinaryOperatorMethods(methodSymbols, "MultiplyLow", x86Avx512FTypeSymbol, RuleKind.op_Multiply);
                AddBinaryOperatorMethods(methodSymbols, "Or", x86Avx512FTypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryOperatorMethods(methodSymbols, "Subtract", x86Avx512FTypeSymbol, RuleKind.op_Subtraction);
                AddBinaryOperatorMethods(methodSymbols, "Xor", x86Avx512FTypeSymbol, RuleKind.op_ExclusiveOr);

                AddShiftOperatorMethods(methodSymbols, "ShiftLeftLogical", x86Avx512FTypeSymbol, RuleKind.op_LeftShift);
                AddShiftOperatorMethods(methodSymbols, "ShiftRightArithmetic", x86Avx512FTypeSymbol, RuleKind.op_RightShift);
                AddShiftOperatorMethods(methodSymbols, "ShiftRightLogical", x86Avx512FTypeSymbol, RuleKind.op_UnsignedRightShift);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx512FVL, out var x86Avx512FVLTypeSymbol))
            {
                AddShiftOperatorMethods(methodSymbols, "ShiftRightArithmetic", x86Avx512FVLTypeSymbol, RuleKind.op_RightShift);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Sse, out var x86SseTypeSymbol))
            {
                AddBinaryOperatorMethods(methodSymbols, "Add", x86SseTypeSymbol, RuleKind.op_Addition);
                AddBinaryOperatorMethods(methodSymbols, "And", x86SseTypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryOperatorMethods(methodSymbols, "Divide", x86SseTypeSymbol, RuleKind.op_Division);
                AddBinaryOperatorMethods(methodSymbols, "Multiply", x86SseTypeSymbol, RuleKind.op_Multiply);
                AddBinaryOperatorMethods(methodSymbols, "Or", x86SseTypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryOperatorMethods(methodSymbols, "Subtract", x86SseTypeSymbol, RuleKind.op_Subtraction);
                AddBinaryOperatorMethods(methodSymbols, "Xor", x86SseTypeSymbol, RuleKind.op_ExclusiveOr);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Sse2, out var x86Sse2TypeSymbol))
            {
                AddBinaryOperatorMethods(methodSymbols, "Add", x86Sse2TypeSymbol, RuleKind.op_Addition);
                AddBinaryOperatorMethods(methodSymbols, "And", x86Sse2TypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryOperatorMethods(methodSymbols, "Divide", x86Sse2TypeSymbol, RuleKind.op_Division);
                AddBinaryOperatorMethods(methodSymbols, "Multiply", x86Sse2TypeSymbol, RuleKind.op_Multiply, [SpecialType.System_Double]);
                AddBinaryOperatorMethods(methodSymbols, "MultiplyLow", x86Sse2TypeSymbol, RuleKind.op_Multiply);
                AddBinaryOperatorMethods(methodSymbols, "Or", x86Sse2TypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryOperatorMethods(methodSymbols, "Subtract", x86Sse2TypeSymbol, RuleKind.op_Subtraction);
                AddBinaryOperatorMethods(methodSymbols, "Xor", x86Sse2TypeSymbol, RuleKind.op_ExclusiveOr);

                AddShiftOperatorMethods(methodSymbols, "ShiftLeftLogical", x86Sse2TypeSymbol, RuleKind.op_LeftShift);
                AddShiftOperatorMethods(methodSymbols, "ShiftRightArithmetic", x86Sse2TypeSymbol, RuleKind.op_RightShift);
                AddShiftOperatorMethods(methodSymbols, "ShiftRightLogical", x86Sse2TypeSymbol, RuleKind.op_UnsignedRightShift);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Sse41, out var x86Sse41TypeSymbol))
            {
                AddBinaryOperatorMethods(methodSymbols, "MultiplyLow", x86Sse41TypeSymbol, RuleKind.op_Multiply);
            }

            if (methodSymbols.Any())
            {
                context.RegisterOperationAction((context) => AnalyzeInvocation(context, methodSymbols), OperationKind.Invocation);
            }

            static void AddBinaryOperatorMethods(Dictionary<IMethodSymbol, RuleKind> methodSymbols, string name, INamedTypeSymbol typeSymbol, RuleKind ruleKind, params SpecialType[] supportedTypes)
            {
                // Looking for a method with 2 operands, where the both are of the same type as the generic return type, such as:
                //    Vector128<byte> Add(Vector128<byte> x, Vector128<byte> y);

                IEnumerable<IMethodSymbol> members =
                    typeSymbol.GetMembers(name)
                              .OfType<IMethodSymbol>()
                              .Where((m) => m.Parameters.Length == 2 &&
                                            m.ReturnType is INamedTypeSymbol namedReturnTypeSymbol &&
                                            namedReturnTypeSymbol.Arity == 1 &&
                                            ((supportedTypes.Length == 0) || supportedTypes.Contains(namedReturnTypeSymbol.TypeArguments[0].SpecialType)) &&
                                            SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, m.Parameters[1].Type) &&
                                            SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, namedReturnTypeSymbol));

                methodSymbols.AddRange(members.Select((m) => new KeyValuePair<IMethodSymbol, RuleKind>(m, ruleKind)));
            }

            static void AddShiftOperatorMethods(Dictionary<IMethodSymbol, RuleKind> methodSymbols, string name, INamedTypeSymbol typeSymbol, RuleKind ruleKind, params SpecialType[] supportedTypes)
            {
                // Looking for a method with 2 operands, where the first is of the same type as the generic return type and the second is byte or int, such as:
                //    Vector128<byte> LeftShift(Vector128<byte> x, byte y);
                //    Vector128<byte> LeftShift(Vector128<byte> x, int y);

                IEnumerable<IMethodSymbol> members =
                    typeSymbol.GetMembers(name)
                              .OfType<IMethodSymbol>()
                              .Where((m) => m.Parameters.Length == 2 &&
                                            m.ReturnType is INamedTypeSymbol namedReturnTypeSymbol &&
                                            namedReturnTypeSymbol.Arity == 1 &&
                                            ((supportedTypes.Length == 0) || supportedTypes.Contains(namedReturnTypeSymbol.TypeArguments[0].SpecialType)) &&
                                            (m.Parameters[1].Type.SpecialType is SpecialType.System_Byte or SpecialType.System_Int32) &&
                                            SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, namedReturnTypeSymbol));

                methodSymbols.AddRange(members.Select((m) => new KeyValuePair<IMethodSymbol, RuleKind>(m, ruleKind)));
            }

            static void AddUnaryOperatorMethods(Dictionary<IMethodSymbol, RuleKind> methodSymbols, string name, INamedTypeSymbol typeSymbol, RuleKind ruleKind, params SpecialType[] supportedTypes)
            {
                // Looking for a method with 1 operand, where it is of the same type as the generic return type, such as:
                //    Vector128<byte> Negate(Vector128<byte> operand);

                IEnumerable<IMethodSymbol> members =
                    typeSymbol.GetMembers(name)
                              .OfType<IMethodSymbol>()
                              .Where((m) => m.Parameters.Length == 1 &&
                                            m.ReturnType is INamedTypeSymbol namedReturnTypeSymbol &&
                                            namedReturnTypeSymbol.Arity == 1 &&
                                            ((supportedTypes.Length == 0) || supportedTypes.Contains(namedReturnTypeSymbol.TypeArguments[0].SpecialType)) &&
                                            SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, namedReturnTypeSymbol));

                methodSymbols.AddRange(members.Select((m) => new KeyValuePair<IMethodSymbol, RuleKind>(m, ruleKind)));
            }
        }

        private void AnalyzeInvocation(OperationAnalysisContext context, Dictionary<IMethodSymbol, RuleKind> methodSymbols)
        {
            if (context.Operation is not IInvocationOperation invocation)
            {
                return;
            }

            IMethodSymbol targetMethod = invocation.TargetMethod;

            if (methodSymbols.TryGetValue(targetMethod, out RuleKind ruleKind) &&
                IsSupported(invocation, ruleKind))
            {
                int i = (int)ruleKind;
                context.ReportDiagnostic(invocation.CreateDiagnostic(Rules[i], Properties[i]));
            }
        }
    }
}
