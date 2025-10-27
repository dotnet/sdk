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

                RuleKind.Abs or
                RuleKind.Ceiling or
                RuleKind.ConvertToInt32 or
                RuleKind.Floor or
                RuleKind.Negate or
                RuleKind.Round or
                RuleKind.Sqrt or
                RuleKind.Truncate => IsValidUnaryMethodInvocation(invocation),

                RuleKind.AddSaturate or
                RuleKind.AndNot or
                RuleKind.AndNot_Swapped or
                RuleKind.Equals or
                RuleKind.GreaterThan or
                RuleKind.GreaterThanOrEqual or
                RuleKind.LessThan or
                RuleKind.LessThanOrEqual or
                RuleKind.Max or
                RuleKind.MaxNative or
                RuleKind.Min or
                RuleKind.MinNative => IsValidBinaryMethodInvocation(invocation),

                RuleKind.ConditionalSelect or
                RuleKind.FusedMultiplyAdd => IsValidTernaryMethodInvocation(invocation),

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

            static bool IsValidUnaryMethodInvocation(IInvocationOperation invocation)
            {
                return (invocation.Arguments.Length == 1) &&
                       SymbolEqualityComparer.Default.Equals(invocation.Type, invocation.Arguments[0].Parameter?.Type);
            }

            static bool IsValidBinaryMethodInvocation(IInvocationOperation invocation)
            {
                return (invocation.Arguments.Length == 2) &&
                       SymbolEqualityComparer.Default.Equals(invocation.Type, invocation.Arguments[0].Parameter?.Type) &&
                       SymbolEqualityComparer.Default.Equals(invocation.Type, invocation.Arguments[1].Parameter?.Type);
            }

            static bool IsValidTernaryMethodInvocation(IInvocationOperation invocation)
            {
                return (invocation.Arguments.Length == 3) &&
                       SymbolEqualityComparer.Default.Equals(invocation.Type, invocation.Arguments[0].Parameter?.Type) &&
                       SymbolEqualityComparer.Default.Equals(invocation.Type, invocation.Arguments[1].Parameter?.Type) &&
                       SymbolEqualityComparer.Default.Equals(invocation.Type, invocation.Arguments[2].Parameter?.Type);
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
                AddBinaryMethods(methodSymbols, "Add", armAdvSimdTypeSymbol, RuleKind.op_Addition);
                AddBinaryMethods(methodSymbols, "AddScalar", armAdvSimdTypeSymbol, RuleKind.op_Addition, [SpecialType.System_Int64, SpecialType.System_UInt64, SpecialType.System_Double]);
                AddBinaryMethods(methodSymbols, "And", armAdvSimdTypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryMethods(methodSymbols, "DivideScalar", armAdvSimdTypeSymbol, RuleKind.op_Division, [SpecialType.System_Double]);
                AddBinaryMethods(methodSymbols, "Multiply", armAdvSimdTypeSymbol, RuleKind.op_Multiply);
                AddBinaryMethods(methodSymbols, "MultiplyScalar", armAdvSimdTypeSymbol, RuleKind.op_Multiply, [SpecialType.System_Double]);
                AddBinaryMethods(methodSymbols, "Or", armAdvSimdTypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryMethods(methodSymbols, "Subtract", armAdvSimdTypeSymbol, RuleKind.op_Subtraction);
                AddBinaryMethods(methodSymbols, "SubtractScalar", armAdvSimdTypeSymbol, RuleKind.op_Subtraction, [SpecialType.System_Int64, SpecialType.System_UInt64, SpecialType.System_Double]);
                AddBinaryMethods(methodSymbols, "Xor", armAdvSimdTypeSymbol, RuleKind.op_ExclusiveOr);

                AddShiftMethods(methodSymbols, "ShiftLeftLogical", armAdvSimdTypeSymbol, RuleKind.op_LeftShift);
                AddShiftMethods(methodSymbols, "ShiftLeftLogicalScalar", armAdvSimdTypeSymbol, RuleKind.op_LeftShift, [SpecialType.System_Int64, SpecialType.System_UInt64]);
                AddShiftMethods(methodSymbols, "ShiftRightArithmetic", armAdvSimdTypeSymbol, RuleKind.op_RightShift);
                AddShiftMethods(methodSymbols, "ShiftRightArithmeticScalar", armAdvSimdTypeSymbol, RuleKind.op_RightShift, [SpecialType.System_Int64, SpecialType.System_UInt64]);
                AddShiftMethods(methodSymbols, "ShiftRightLogical", armAdvSimdTypeSymbol, RuleKind.op_UnsignedRightShift);
                AddShiftMethods(methodSymbols, "ShiftRightLogicalScalar", armAdvSimdTypeSymbol, RuleKind.op_UnsignedRightShift, [SpecialType.System_Int64, SpecialType.System_UInt64]);

                AddUnaryMethods(methodSymbols, "Negate", armAdvSimdTypeSymbol, RuleKind.op_UnaryNegation);
                AddUnaryMethods(methodSymbols, "NegateScalar", armAdvSimdTypeSymbol, RuleKind.op_UnaryNegation, [SpecialType.System_Double]);
                AddUnaryMethods(methodSymbols, "Not", armAdvSimdTypeSymbol, RuleKind.op_OnesComplement);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsArmAdvSimdArm64, out var armAdvSimdArm64TypeSymbol))
            {
                AddBinaryMethods(methodSymbols, "Add", armAdvSimdArm64TypeSymbol, RuleKind.op_Addition);
                AddBinaryMethods(methodSymbols, "Divide", armAdvSimdArm64TypeSymbol, RuleKind.op_Division);
                AddBinaryMethods(methodSymbols, "Multiply", armAdvSimdArm64TypeSymbol, RuleKind.op_Multiply);
                AddBinaryMethods(methodSymbols, "Subtract", armAdvSimdArm64TypeSymbol, RuleKind.op_Subtraction);

                AddUnaryMethods(methodSymbols, "Negate", armAdvSimdArm64TypeSymbol, RuleKind.op_UnaryNegation);
                AddUnaryMethods(methodSymbols, "NegateScalar", armAdvSimdArm64TypeSymbol, RuleKind.op_UnaryNegation, [SpecialType.System_Int64]);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsWasmPackedSimd, out var wasmPackedSimdTypeSymbol))
            {
                AddBinaryMethods(methodSymbols, "Add", wasmPackedSimdTypeSymbol, RuleKind.op_Addition);
                AddBinaryMethods(methodSymbols, "And", wasmPackedSimdTypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryMethods(methodSymbols, "Divide", wasmPackedSimdTypeSymbol, RuleKind.op_Division);
                AddBinaryMethods(methodSymbols, "Multiply", wasmPackedSimdTypeSymbol, RuleKind.op_Multiply);
                AddBinaryMethods(methodSymbols, "Or", wasmPackedSimdTypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryMethods(methodSymbols, "Subtract", wasmPackedSimdTypeSymbol, RuleKind.op_Subtraction);
                AddBinaryMethods(methodSymbols, "Xor", wasmPackedSimdTypeSymbol, RuleKind.op_ExclusiveOr);

                AddShiftMethods(methodSymbols, "ShiftLeft", wasmPackedSimdTypeSymbol, RuleKind.op_LeftShift);
                AddShiftMethods(methodSymbols, "ShiftRightArithmetic", wasmPackedSimdTypeSymbol, RuleKind.op_RightShift);
                AddShiftMethods(methodSymbols, "ShiftRightLogical", wasmPackedSimdTypeSymbol, RuleKind.op_UnsignedRightShift);

                AddUnaryMethods(methodSymbols, "Negate", wasmPackedSimdTypeSymbol, RuleKind.op_UnaryNegation);
                AddUnaryMethods(methodSymbols, "Not", wasmPackedSimdTypeSymbol, RuleKind.op_OnesComplement);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx, out var x86AvxTypeSymbol))
            {
                AddBinaryMethods(methodSymbols, "Add", x86AvxTypeSymbol, RuleKind.op_Addition);
                AddBinaryMethods(methodSymbols, "And", x86AvxTypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryMethods(methodSymbols, "Divide", x86AvxTypeSymbol, RuleKind.op_Division);
                AddBinaryMethods(methodSymbols, "Multiply", x86AvxTypeSymbol, RuleKind.op_Multiply);
                AddBinaryMethods(methodSymbols, "Or", x86AvxTypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryMethods(methodSymbols, "Subtract", x86AvxTypeSymbol, RuleKind.op_Subtraction);
                AddBinaryMethods(methodSymbols, "Xor", x86AvxTypeSymbol, RuleKind.op_ExclusiveOr);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx2, out var x86Avx2TypeSymbol))
            {
                AddBinaryMethods(methodSymbols, "Add", x86Avx2TypeSymbol, RuleKind.op_Addition);
                AddBinaryMethods(methodSymbols, "And", x86Avx2TypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryMethods(methodSymbols, "MultiplyLow", x86Avx2TypeSymbol, RuleKind.op_Multiply);
                AddBinaryMethods(methodSymbols, "Or", x86Avx2TypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryMethods(methodSymbols, "Subtract", x86Avx2TypeSymbol, RuleKind.op_Subtraction);
                AddBinaryMethods(methodSymbols, "Xor", x86Avx2TypeSymbol, RuleKind.op_ExclusiveOr);

                AddShiftMethods(methodSymbols, "ShiftLeftLogical", x86Avx2TypeSymbol, RuleKind.op_LeftShift);
                AddShiftMethods(methodSymbols, "ShiftRightArithmetic", x86Avx2TypeSymbol, RuleKind.op_RightShift);
                AddShiftMethods(methodSymbols, "ShiftRightLogical", x86Avx2TypeSymbol, RuleKind.op_UnsignedRightShift);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx512BW, out var x86Avx512BWTypeSymbol))
            {
                AddBinaryMethods(methodSymbols, "Add", x86Avx512BWTypeSymbol, RuleKind.op_Addition);
                AddBinaryMethods(methodSymbols, "MultiplyLow", x86Avx512BWTypeSymbol, RuleKind.op_Multiply);
                AddBinaryMethods(methodSymbols, "Subtract", x86Avx512BWTypeSymbol, RuleKind.op_Subtraction);

                AddShiftMethods(methodSymbols, "ShiftLeftLogical", x86Avx512BWTypeSymbol, RuleKind.op_LeftShift);
                AddShiftMethods(methodSymbols, "ShiftRightArithmetic", x86Avx512BWTypeSymbol, RuleKind.op_RightShift);
                AddShiftMethods(methodSymbols, "ShiftRightLogical", x86Avx512BWTypeSymbol, RuleKind.op_UnsignedRightShift);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx512DQ, out var x86Avx512DQTypeSymbol))
            {
                AddBinaryMethods(methodSymbols, "And", x86Avx512DQTypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryMethods(methodSymbols, "MultiplyLow", x86Avx512DQTypeSymbol, RuleKind.op_Multiply);
                AddBinaryMethods(methodSymbols, "Or", x86Avx512DQTypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryMethods(methodSymbols, "Xor", x86Avx512DQTypeSymbol, RuleKind.op_ExclusiveOr);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx512DQVL, out var x86Avx512DQVLTypeSymbol))
            {
                AddBinaryMethods(methodSymbols, "MultiplyLow", x86Avx512DQVLTypeSymbol, RuleKind.op_Multiply);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx512F, out var x86Avx512FTypeSymbol))
            {
                AddBinaryMethods(methodSymbols, "Add", x86Avx512FTypeSymbol, RuleKind.op_Addition);
                AddBinaryMethods(methodSymbols, "And", x86Avx512FTypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryMethods(methodSymbols, "Divide", x86Avx512FTypeSymbol, RuleKind.op_Division);
                AddBinaryMethods(methodSymbols, "Multiply", x86Avx512FTypeSymbol, RuleKind.op_Multiply, [SpecialType.System_Single, SpecialType.System_Double]);
                AddBinaryMethods(methodSymbols, "MultiplyLow", x86Avx512FTypeSymbol, RuleKind.op_Multiply);
                AddBinaryMethods(methodSymbols, "Or", x86Avx512FTypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryMethods(methodSymbols, "Subtract", x86Avx512FTypeSymbol, RuleKind.op_Subtraction);
                AddBinaryMethods(methodSymbols, "Xor", x86Avx512FTypeSymbol, RuleKind.op_ExclusiveOr);

                AddShiftMethods(methodSymbols, "ShiftLeftLogical", x86Avx512FTypeSymbol, RuleKind.op_LeftShift);
                AddShiftMethods(methodSymbols, "ShiftRightArithmetic", x86Avx512FTypeSymbol, RuleKind.op_RightShift);
                AddShiftMethods(methodSymbols, "ShiftRightLogical", x86Avx512FTypeSymbol, RuleKind.op_UnsignedRightShift);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx512FVL, out var x86Avx512FVLTypeSymbol))
            {
                AddShiftMethods(methodSymbols, "ShiftRightArithmetic", x86Avx512FVLTypeSymbol, RuleKind.op_RightShift);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Sse, out var x86SseTypeSymbol))
            {
                AddBinaryMethods(methodSymbols, "Add", x86SseTypeSymbol, RuleKind.op_Addition);
                AddBinaryMethods(methodSymbols, "And", x86SseTypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryMethods(methodSymbols, "Divide", x86SseTypeSymbol, RuleKind.op_Division);
                AddBinaryMethods(methodSymbols, "Multiply", x86SseTypeSymbol, RuleKind.op_Multiply);
                AddBinaryMethods(methodSymbols, "Or", x86SseTypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryMethods(methodSymbols, "Subtract", x86SseTypeSymbol, RuleKind.op_Subtraction);
                AddBinaryMethods(methodSymbols, "Xor", x86SseTypeSymbol, RuleKind.op_ExclusiveOr);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Sse2, out var x86Sse2TypeSymbol))
            {
                AddBinaryMethods(methodSymbols, "Add", x86Sse2TypeSymbol, RuleKind.op_Addition);
                AddBinaryMethods(methodSymbols, "And", x86Sse2TypeSymbol, RuleKind.op_BitwiseAnd);
                AddBinaryMethods(methodSymbols, "AndNot", x86Sse2TypeSymbol, RuleKind.AndNot_Swapped);
                AddBinaryMethods(methodSymbols, "Divide", x86Sse2TypeSymbol, RuleKind.op_Division);
                AddBinaryMethods(methodSymbols, "Multiply", x86Sse2TypeSymbol, RuleKind.op_Multiply, [SpecialType.System_Double]);
                AddBinaryMethods(methodSymbols, "MultiplyLow", x86Sse2TypeSymbol, RuleKind.op_Multiply);
                AddBinaryMethods(methodSymbols, "Or", x86Sse2TypeSymbol, RuleKind.op_BitwiseOr);
                AddBinaryMethods(methodSymbols, "Subtract", x86Sse2TypeSymbol, RuleKind.op_Subtraction);
                AddBinaryMethods(methodSymbols, "Xor", x86Sse2TypeSymbol, RuleKind.op_ExclusiveOr);

                AddShiftMethods(methodSymbols, "ShiftLeftLogical", x86Sse2TypeSymbol, RuleKind.op_LeftShift);
                AddShiftMethods(methodSymbols, "ShiftRightArithmetic", x86Sse2TypeSymbol, RuleKind.op_RightShift);
                AddShiftMethods(methodSymbols, "ShiftRightLogical", x86Sse2TypeSymbol, RuleKind.op_UnsignedRightShift);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Sse41, out var x86Sse41TypeSymbol))
            {
                AddBinaryMethods(methodSymbols, "MultiplyLow", x86Sse41TypeSymbol, RuleKind.op_Multiply);
            }

            // Register named methods (not operators) that have cross-platform equivalents

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsArmAdvSimd, out var armAdvSimdTypeSymbolForMethods))
            {
                // Note: AdvSimd.Abs for integer types returns unsigned types (e.g., Vector128<int> → Vector128<uint>),
                // so we only register floating-point Abs which has compatible signatures
                AddUnaryMethods(methodSymbols, "Abs", armAdvSimdTypeSymbolForMethods, RuleKind.Abs, [SpecialType.System_Single]);
                AddUnaryMethods(methodSymbols, "AbsScalar", armAdvSimdTypeSymbolForMethods, RuleKind.Abs, [SpecialType.System_Double]);
                AddBinaryMethods(methodSymbols, "AddSaturate", armAdvSimdTypeSymbolForMethods, RuleKind.AddSaturate);
                AddBinaryMethods(methodSymbols, "BitwiseClear", armAdvSimdTypeSymbolForMethods, RuleKind.AndNot);
                AddTernaryMethods(methodSymbols, "BitwiseSelect", armAdvSimdTypeSymbolForMethods, RuleKind.ConditionalSelect);
                AddUnaryMethods(methodSymbols, "Ceiling", armAdvSimdTypeSymbolForMethods, RuleKind.Ceiling);
                AddUnaryMethods(methodSymbols, "CeilingScalar", armAdvSimdTypeSymbolForMethods, RuleKind.Ceiling);
                AddBinaryMethods(methodSymbols, "CompareEqual", armAdvSimdTypeSymbolForMethods, RuleKind.Equals);
                AddBinaryMethods(methodSymbols, "CompareGreaterThan", armAdvSimdTypeSymbolForMethods, RuleKind.GreaterThan);
                AddBinaryMethods(methodSymbols, "CompareGreaterThanOrEqual", armAdvSimdTypeSymbolForMethods, RuleKind.GreaterThanOrEqual);
                AddBinaryMethods(methodSymbols, "CompareLessThan", armAdvSimdTypeSymbolForMethods, RuleKind.LessThan);
                AddBinaryMethods(methodSymbols, "CompareLessThanOrEqual", armAdvSimdTypeSymbolForMethods, RuleKind.LessThanOrEqual);
                AddUnaryMethods(methodSymbols, "ConvertToInt32RoundToZero", armAdvSimdTypeSymbolForMethods, RuleKind.ConvertToInt32);
                AddUnaryMethods(methodSymbols, "ConvertToInt32RoundToZeroScalar", armAdvSimdTypeSymbolForMethods, RuleKind.ConvertToInt32);
                // Extract maps to GetElement - needs index parameter handling in fixer
                // TODO: Implement Extract → GetElement transformation
                AddUnaryMethods(methodSymbols, "Floor", armAdvSimdTypeSymbolForMethods, RuleKind.Floor);
                AddUnaryMethods(methodSymbols, "FloorScalar", armAdvSimdTypeSymbolForMethods, RuleKind.Floor);
                AddTernaryMethods(methodSymbols, "FusedMultiplyAdd", armAdvSimdTypeSymbolForMethods, RuleKind.FusedMultiplyAdd);
                // FusedMultiplySubtract and variants need parameter negation/reordering in fixer
                // TODO: Implement FusedMultiplySubtract transformations
                // Insert maps to WithElement - needs index parameter handling in fixer
                // TODO: Implement Insert → WithElement transformation
                AddBinaryMethods(methodSymbols, "Max", armAdvSimdTypeSymbolForMethods, RuleKind.Max);
                AddBinaryMethods(methodSymbols, "Min", armAdvSimdTypeSymbolForMethods, RuleKind.Min);
                // Note: Negate is already registered as op_UnaryNegation above, so we don't register it here
                // Note: MultiplyAdd(x, y, z) expands to (y * z) + x - needs operator expansion
                // TODO: Implement MultiplyAdd → operator expansion transformation
                // Note: OrNot expands to two operators - needs operator expansion  
                // TODO: Implement OrNot → operator expansion transformation
                AddUnaryMethods(methodSymbols, "RoundToNearest", armAdvSimdTypeSymbolForMethods, RuleKind.Round);
                AddUnaryMethods(methodSymbols, "RoundToNegativeInfinity", armAdvSimdTypeSymbolForMethods, RuleKind.Floor);
                AddUnaryMethods(methodSymbols, "RoundToPositiveInfinity", armAdvSimdTypeSymbolForMethods, RuleKind.Ceiling);
                AddUnaryMethods(methodSymbols, "RoundToZero", armAdvSimdTypeSymbolForMethods, RuleKind.Truncate);
                // Note: Sqrt is already registered above as operator method
                // Note: Load*/Store*/Shuffle/etc need more complex transformations - will address separately
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsArmAdvSimdArm64, out var armAdvSimdArm64TypeSymbolForMethods))
            {
                // Note: AdvSimd.Arm64.Abs for integer types returns unsigned types (e.g., Vector128<long> → Vector128<ulong>),
                // so we only register AbsScalar for compatible types
                AddUnaryMethods(methodSymbols, "AbsScalar", armAdvSimdArm64TypeSymbolForMethods, RuleKind.Abs, [SpecialType.System_Double]);
                AddBinaryMethods(methodSymbols, "AddSaturateScalar", armAdvSimdArm64TypeSymbolForMethods, RuleKind.AddSaturate);
                // Note: Negate is already registered as op_UnaryNegation above, so we don't register it here
                // TODO: Add scalar variants of Extract/Insert, FusedMultiply*, Round*
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsWasmPackedSimd, out var wasmPackedSimdTypeSymbolForMethods))
            {
                AddUnaryMethods(methodSymbols, "Abs", wasmPackedSimdTypeSymbolForMethods, RuleKind.Abs);
                AddUnaryMethods(methodSymbols, "Ceiling", wasmPackedSimdTypeSymbolForMethods, RuleKind.Ceiling);
                AddUnaryMethods(methodSymbols, "Floor", wasmPackedSimdTypeSymbolForMethods, RuleKind.Floor);
                AddBinaryMethods(methodSymbols, "Max", wasmPackedSimdTypeSymbolForMethods, RuleKind.Max);
                AddBinaryMethods(methodSymbols, "Min", wasmPackedSimdTypeSymbolForMethods, RuleKind.Min);
                // Note: Negate is already registered as op_UnaryNegation above, so we don't register it here
                AddUnaryMethods(methodSymbols, "Sqrt", wasmPackedSimdTypeSymbolForMethods, RuleKind.Sqrt);
                AddUnaryMethods(methodSymbols, "Truncate", wasmPackedSimdTypeSymbolForMethods, RuleKind.Truncate);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx, out var x86AvxTypeSymbolForMethods))
            {
                AddBinaryMethods(methodSymbols, "AndNot", x86AvxTypeSymbolForMethods, RuleKind.AndNot_Swapped);
                AddUnaryMethods(methodSymbols, "Ceiling", x86AvxTypeSymbolForMethods, RuleKind.Ceiling);
                AddUnaryMethods(methodSymbols, "Floor", x86AvxTypeSymbolForMethods, RuleKind.Floor);
                AddBinaryMethods(methodSymbols, "Max", x86AvxTypeSymbolForMethods, RuleKind.MaxNative);
                AddBinaryMethods(methodSymbols, "Min", x86AvxTypeSymbolForMethods, RuleKind.MinNative);
                AddUnaryMethods(methodSymbols, "RoundToNearestInteger", x86AvxTypeSymbolForMethods, RuleKind.Round);
                AddUnaryMethods(methodSymbols, "RoundToNegativeInfinity", x86AvxTypeSymbolForMethods, RuleKind.Floor);
                AddUnaryMethods(methodSymbols, "RoundToPositiveInfinity", x86AvxTypeSymbolForMethods, RuleKind.Ceiling);
                AddUnaryMethods(methodSymbols, "RoundToZero", x86AvxTypeSymbolForMethods, RuleKind.Truncate);
                AddUnaryMethods(methodSymbols, "Sqrt", x86AvxTypeSymbolForMethods, RuleKind.Sqrt);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx2, out var x86Avx2TypeSymbolForMethods))
            {
                AddUnaryMethods(methodSymbols, "Abs", x86Avx2TypeSymbolForMethods, RuleKind.Abs);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx512BW, out var x86Avx512BWTypeSymbolForMethods))
            {
                AddUnaryMethods(methodSymbols, "Abs", x86Avx512BWTypeSymbolForMethods, RuleKind.Abs);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Avx512F, out var x86Avx512FTypeSymbolForMethods))
            {
                AddUnaryMethods(methodSymbols, "Abs", x86Avx512FTypeSymbolForMethods, RuleKind.Abs);
                AddTernaryMethods(methodSymbols, "FusedMultiplyAdd", x86Avx512FTypeSymbolForMethods, RuleKind.FusedMultiplyAdd);
                AddBinaryMethods(methodSymbols, "Max", x86Avx512FTypeSymbolForMethods, RuleKind.MaxNative);
                AddBinaryMethods(methodSymbols, "Min", x86Avx512FTypeSymbolForMethods, RuleKind.MinNative);
                AddUnaryMethods(methodSymbols, "RoundToNearestInteger", x86Avx512FTypeSymbolForMethods, RuleKind.Round);
                AddUnaryMethods(methodSymbols, "Sqrt", x86Avx512FTypeSymbolForMethods, RuleKind.Sqrt);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Fma, out var x86FmaTypeSymbolForMethods))
            {
                AddTernaryMethods(methodSymbols, "MultiplyAdd", x86FmaTypeSymbolForMethods, RuleKind.FusedMultiplyAdd);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Sse, out var x86SseTypeSymbolForMethods))
            {
                AddBinaryMethods(methodSymbols, "AndNot", x86SseTypeSymbolForMethods, RuleKind.AndNot_Swapped);
                AddBinaryMethods(methodSymbols, "Max", x86SseTypeSymbolForMethods, RuleKind.MaxNative);
                AddBinaryMethods(methodSymbols, "Min", x86SseTypeSymbolForMethods, RuleKind.MinNative);
                AddUnaryMethods(methodSymbols, "Sqrt", x86SseTypeSymbolForMethods, RuleKind.Sqrt);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Sse2, out var x86Sse2TypeSymbolForMethods))
            {
                AddUnaryMethods(methodSymbols, "Sqrt", x86Sse2TypeSymbolForMethods, RuleKind.Sqrt);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Sse41, out var x86Sse41TypeSymbolForMethods))
            {
                AddUnaryMethods(methodSymbols, "RoundToNearestInteger", x86Sse41TypeSymbolForMethods, RuleKind.Round);
                AddUnaryMethods(methodSymbols, "RoundToNegativeInfinity", x86Sse41TypeSymbolForMethods, RuleKind.Floor);
                AddUnaryMethods(methodSymbols, "RoundToPositiveInfinity", x86Sse41TypeSymbolForMethods, RuleKind.Ceiling);
                AddUnaryMethods(methodSymbols, "RoundToZero", x86Sse41TypeSymbolForMethods, RuleKind.Truncate);
            }

            if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeIntrinsicsX86Ssse3, out var x86Ssse3TypeSymbolForMethods))
            {
                AddUnaryMethods(methodSymbols, "Abs", x86Ssse3TypeSymbolForMethods, RuleKind.Abs);
            }

            if (methodSymbols.Any())
            {
                context.RegisterOperationAction((context) => AnalyzeInvocation(context, methodSymbols), OperationKind.Invocation);
            }

            static void AddBinaryMethods(Dictionary<IMethodSymbol, RuleKind> methodSymbols, string name, INamedTypeSymbol typeSymbol, RuleKind ruleKind, params SpecialType[] supportedTypes)
            {
                // Looking for a method with 2 operands, where both are of the same type as the generic return type, such as:
                //    Vector128<byte> Add(Vector128<byte> x, Vector128<byte> y);
                // This is used for both operator replacements (e.g., Add → +) and method replacements (e.g., Max → Vector128.Max)

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

            static void AddShiftMethods(Dictionary<IMethodSymbol, RuleKind> methodSymbols, string name, INamedTypeSymbol typeSymbol, RuleKind ruleKind, params SpecialType[] supportedTypes)
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

            static void AddUnaryMethods(Dictionary<IMethodSymbol, RuleKind> methodSymbols, string name, INamedTypeSymbol typeSymbol, RuleKind ruleKind, params SpecialType[] supportedTypes)
            {
                // Looking for a method with 1 operand, where it is of the same type as the generic return type, such as:
                //    Vector128<byte> Negate(Vector128<byte> operand);
                // This is used for both operator replacements (e.g., Negate → unary -) and method replacements (e.g., Sqrt → Vector128.Sqrt)

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

            static void AddTernaryMethods(Dictionary<IMethodSymbol, RuleKind> methodSymbols, string name, INamedTypeSymbol typeSymbol, RuleKind ruleKind, params SpecialType[] supportedTypes)
            {
                // Looking for a method with 3 operands, where all are of the same type as the generic return type, such as:
                //    Vector128<float> FusedMultiplyAdd(Vector128<float> a, Vector128<float> b, Vector128<float> c);

                IEnumerable<IMethodSymbol> members =
                    typeSymbol.GetMembers(name)
                              .OfType<IMethodSymbol>()
                              .Where((m) => m.Parameters.Length == 3 &&
                                            m.ReturnType is INamedTypeSymbol namedReturnTypeSymbol &&
                                            namedReturnTypeSymbol.Arity == 1 &&
                                            ((supportedTypes.Length == 0) || supportedTypes.Contains(namedReturnTypeSymbol.TypeArguments[0].SpecialType)) &&
                                            SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, namedReturnTypeSymbol) &&
                                            SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, namedReturnTypeSymbol) &&
                                            SymbolEqualityComparer.Default.Equals(m.Parameters[2].Type, namedReturnTypeSymbol));

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
