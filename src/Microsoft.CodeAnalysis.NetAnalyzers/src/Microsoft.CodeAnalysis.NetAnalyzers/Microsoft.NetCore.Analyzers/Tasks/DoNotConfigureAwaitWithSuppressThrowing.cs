// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Tasks
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2261: <inheritdoc cref="DoNotUseConfigureAwaitWithSuppressThrowingTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseConfigureAwaitWithSuppressThrowing : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2261";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
            CreateLocalizableResourceString(nameof(DoNotUseConfigureAwaitWithSuppressThrowingTitle)),
            CreateLocalizableResourceString(nameof(DoNotUseConfigureAwaitWithSuppressThrowingMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.BuildWarning,
            CreateLocalizableResourceString(nameof(DoNotUseConfigureAwaitWithSuppressThrowingDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(compilationContext =>
            {
                // Only proceed if we have the Task<T>.ConfigureAwait(ConfigureAwaitOptions) method and if ConfigureAwaitOptions.SuppressThrowing is defined.
                if (!compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask1, out var genericTask) ||
                    !compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksConfigureAwaitOptions, out var configureAwaitOptionsType) ||
                    configureAwaitOptionsType.TypeKind != TypeKind.Enum ||
                    genericTask
                        .GetMembers("ConfigureAwait")
                        .OfType<IMethodSymbol>()
                        .Where(m => SymbolEqualityComparer.Default.Equals(m.ContainingType, genericTask) && m.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, configureAwaitOptionsType))
                        .FirstOrDefault() is not IMethodSymbol configureAwait ||
                    configureAwaitOptionsType.GetMembers("SuppressThrowing").FirstOrDefault() is not IFieldSymbol suppressThrowing ||
                    !DiagnosticHelpers.TryConvertToUInt64(suppressThrowing.ConstantValue, configureAwaitOptionsType.EnumUnderlyingType!.SpecialType, out ulong suppressThrowingValue))
                {
                    return;
                }

                // Raise a diagnostic if the invocation is to Task<T>.ConfigureAwait with a constant value that includes SuppressThrowing
                compilationContext.RegisterOperationAction(operationContext =>
                {
                    IInvocationOperation invocation = (IInvocationOperation)operationContext.Operation;

                    if (SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.OriginalDefinition, configureAwait) &&
                        invocation.Arguments.Length == 1 &&
                        invocation.Arguments[0].Value is IOperation { ConstantValue.HasValue: true } arg &&
                        DiagnosticHelpers.TryConvertToUInt64(arg.ConstantValue.Value, configureAwaitOptionsType.EnumUnderlyingType.SpecialType, out ulong argValue) &&
                        (argValue & suppressThrowingValue) != 0)
                    {
                        operationContext.ReportDiagnostic(arg.CreateDiagnostic(Rule));
                    }
                }, OperationKind.Invocation);
            });
        }
    }
}