// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1855: <inheritdoc cref="UseSpanClearInsteadOfFillTitle"/>
    /// </summary>
    public abstract class UseSpanClearInsteadOfFillAnalyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA1855";
        internal const string FillMethod = "Fill";
        internal const string ClearMethod = "Clear";

        internal static readonly DiagnosticDescriptor s_Rule = DiagnosticDescriptorHelper.Create(
            DiagnosticId,
            CreateLocalizableResourceString(nameof(UseSpanClearInsteadOfFillTitle)),
            CreateLocalizableResourceString(nameof(UseSpanClearInsteadOfFillMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(UseSpanClearInsteadOfFillDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(s_Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var compilation = context.Compilation;
            if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSpan1, out var spanType))
            {
                return;
            }

            var spanFillMethod = spanType.GetMembers(FillMethod)
                .OfType<IMethodSymbol>()
                .FirstOrDefault();

            if (spanFillMethod?.Parameters.Length != 1)
            {
                return;
            }

            context.RegisterOperationAction(
                context =>
                {
                    var invocation = (IInvocationOperation)context.Operation;

                    if (SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.OriginalDefinition, spanFillMethod)
                        && invocation.Arguments.Length == 1
                        && IsDefaultValue(invocation.Arguments[0]))
                    {
                        context.ReportDiagnostic(invocation.CreateDiagnostic(s_Rule));
                    }
                },
                OperationKind.Invocation);
        }

        private static bool IsDefaultValue(IArgumentOperation argumentOperation)
        {
            var value = argumentOperation.Value;
            var type = argumentOperation.Parameter?.Type;
            var constantOpt = value.ConstantValue;

            if (constantOpt.HasValue)
            {
                if (constantOpt.Value == null)
                {
                    // null must be default value for any valid type
                    return true;
                }

                if (argumentOperation.Type.IsNullableValueType())
                {
                    // 0 isn't default value for T?
                    return false;
                }

                // enum/nint are treated as integers
                // This is missing default value of DateTime literal for VB,
                // but since VB doesn't properly support Span, just don't consider it
                switch (constantOpt.Value)
                {
                    case (byte)0 or (short)0 or 0 or 0L or
                        (sbyte)0 or (ushort)0 or 0U or 0UL or
                        false or '\0':
                        return true;

                    // -0 is not all bits zero. Handle them by bits
                    case float f:
                        // SingleToInt32Bits not available in netstandard2.0
                        return BitConverter.DoubleToInt64Bits(f) == 0;

                    case double d:
                        return BitConverter.DoubleToInt64Bits(d) == 0;

                    case decimal d:
                        return decimal.GetBits(d).All(b => b == 0);

                    default:
                        return false;
                }
            }

            if (value is IDefaultValueOperation)
            {
                return SymbolEqualityComparer.Default.Equals(value.Type, type);
            }

            if (value is IConversionOperation { Operand: IDefaultValueOperation defaultValue })
            {
                // handle the conversion of default literal only
                return SymbolEqualityComparer.Default.Equals(defaultValue.Type, type);
            }

            if (value is IObjectCreationOperation objectCreation)
            {
                return objectCreation.Type?.IsValueType == true
                    && objectCreation.Constructor?.IsImplicitlyDeclared == true
                    && objectCreation.Arguments.IsEmpty
                    && (objectCreation.Initializer?.Initializers.IsEmpty ?? true)
                    && SymbolEqualityComparer.Default.Equals(value.Type, type);
            }

            return false;
        }
    }
}
