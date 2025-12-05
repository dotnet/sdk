// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1508: <inheritdoc cref="AvoidDeadConditionalCodeTitle"/>
    /// 
    /// Flags conditional expressions which are always true/false and null checks for operations that are always null/non-null based on predicate analysis.
    /// </summary>
    public abstract class AvoidDeadConditionalCode : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1508";

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(AvoidDeadConditionalCodeTitle));

        // https://github.com/dotnet/roslyn-analyzers/issues/2180 tracks enabling the rule by default

        internal static readonly DiagnosticDescriptor AlwaysTrueFalseOrNullRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(AvoidDeadConditionalCodeAlwaysTruFalseOrNullMessage)),
            DiagnosticCategory.Maintainability,
            RuleLevel.Disabled,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: true);

        internal static readonly DiagnosticDescriptor NeverNullRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(AvoidDeadConditionalCodeNeverNullMessage)),
            DiagnosticCategory.Maintainability,
            RuleLevel.Disabled,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(AlwaysTrueFalseOrNullRule, NeverNullRule);

        protected abstract bool IsSwitchArmExpressionWithWhenClause(SyntaxNode node);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                var typeProvider = WellKnownTypeProvider.GetOrCreate(compilationContext.Compilation);
                var debugAssert = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsDebug);
                var debugAssertMethods = debugAssert?.GetMembers(nameof(Debug.Assert)).OfType<IMethodSymbol>().ToArray() ?? Array.Empty<IMethodSymbol>();
                compilationContext.RegisterOperationBlockAction(operationBlockContext =>
                {
                    var owningSymbol = operationBlockContext.OwningSymbol;
                    if (operationBlockContext.Options.IsConfiguredToSkipAnalysis(AlwaysTrueFalseOrNullRule, owningSymbol, operationBlockContext.Compilation))
                    {
                        return;
                    }

                    var processedOperationRoots = new HashSet<IOperation>();

                    foreach (var operationRoot in operationBlockContext.OperationBlocks)
                    {
                        bool ShouldAnalyze(IOperation op) =>
                                ((op as IBinaryOperation)?.IsComparisonOperator() == true ||
                                op is IInvocationOperation { TargetMethod.ReturnType.SpecialType: SpecialType.System_Boolean } ||
                                op.Kind == OperationKind.Coalesce ||
                                op.Kind == OperationKind.ConditionalAccess ||
                                op.Kind == OperationKind.IsNull ||
                                op.Kind == OperationKind.IsPattern)
                                && (op.Parent is not IArgumentOperation { Parent: IInvocationOperation invocation } ||
                                    !debugAssertMethods.Contains(invocation.TargetMethod, SymbolEqualityComparer.Default));

                        if (operationRoot.HasAnyOperationDescendant(ShouldAnalyze))
                        {
                            // Skip duplicate analysis from operation blocks for constructor initializer and body.
                            if (!processedOperationRoots.Add(operationRoot.GetRoot()))
                            {
                                // Already processed.
                                continue;
                            }

                            var cfg = operationBlockContext.GetControlFlowGraph(operationRoot);
                            var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(operationBlockContext.Compilation);
                            var valueContentAnalysisResult = ValueContentAnalysis.TryGetOrComputeResult(cfg, owningSymbol, wellKnownTypeProvider,
                                    operationBlockContext.Options, AlwaysTrueFalseOrNullRule,
                                    PointsToAnalysisKind.Complete,
                                    out var copyAnalysisResultOpt,
                                    out var pointsToAnalysisResult);
                            if (valueContentAnalysisResult == null ||
                                pointsToAnalysisResult == null)
                            {
                                continue;
                            }

                            foreach (var block in cfg.Blocks)
                            {
                                foreach (var operation in block.DescendantOperations())
                                {
                                    // Skip implicit operations.
                                    // However, 'IsNull' operations are compiler generated operations corresponding to
                                    // non-implicit conditional access operations, so we should not skip them.
                                    if (operation.IsImplicit && operation.Kind != OperationKind.IsNull)
                                    {
                                        continue;
                                    }

                                    switch (operation.Kind)
                                    {
                                        case OperationKind.BinaryOperator:
                                            var binaryOperation = (IBinaryOperation)operation;
                                            PredicateValueKind predicateKind = GetPredicateKind(binaryOperation);
                                            if (predicateKind != PredicateValueKind.Unknown &&
                                                (binaryOperation.LeftOperand is not IBinaryOperation leftBinary || GetPredicateKind(leftBinary) == PredicateValueKind.Unknown) &&
                                                (binaryOperation.RightOperand is not IBinaryOperation rightBinary || GetPredicateKind(rightBinary) == PredicateValueKind.Unknown))
                                            {
                                                ReportAlwaysTrueFalseOrNullDiagnostic(operation, predicateKind);
                                            }

                                            break;

                                        case OperationKind.Invocation:
                                            predicateKind = GetPredicateKind(operation);
                                            if (predicateKind != PredicateValueKind.Unknown)
                                            {
                                                ReportAlwaysTrueFalseOrNullDiagnostic(operation, predicateKind);
                                            }

                                            break;

                                        case OperationKind.IsPattern:
                                            predicateKind = GetPredicateKind(operation);

                                            // We bail out for lowered IsPatternOperation generated for a switch arm expression with a when clause.
                                            // We currently perform syntactic checks as the switch arm and when clause are split into
                                            // multiple basic blocks and control flow branches during CFG generation.
                                            // TODO: Consider if we can harden this validation without relying on syntactic checks.
                                            if (predicateKind != PredicateValueKind.Unknown &&
                                                !IsSwitchArmExpressionWithWhenClause(operation.Syntax))
                                            {
                                                ReportAlwaysTrueFalseOrNullDiagnostic(operation, predicateKind);
                                            }

                                            break;

                                        case OperationKind.IsNull:
                                            // '{0}' is always/never '{1}'. Remove or refactor the condition(s) to avoid dead code.
                                            predicateKind = GetPredicateKind(operation);
                                            DiagnosticDescriptor rule;
                                            switch (predicateKind)
                                            {
                                                case PredicateValueKind.AlwaysTrue:
                                                    rule = AlwaysTrueFalseOrNullRule;
                                                    break;

                                                case PredicateValueKind.AlwaysFalse:
                                                    rule = NeverNullRule;
                                                    break;

                                                default:
                                                    continue;
                                            }

                                            // Skip IsNull operations in compiler generated finally region for using statement
                                            if (block.IsFirstBlockOfCompilerGeneratedFinally(cfg))
                                            {
                                                continue;
                                            }

                                            var arg1 = operation.Syntax.ToString();
                                            var arg2 = operation.Language == LanguageNames.VisualBasic ? "Nothing" : "null";
                                            var diagnostic = operation.CreateDiagnostic(rule, arg1, arg2);
                                            operationBlockContext.ReportDiagnostic(diagnostic);
                                            break;
                                    }
                                }
                            }

                            PredicateValueKind GetPredicateKind(IOperation operation)
                            {
                                Debug.Assert(operation.Kind is OperationKind.BinaryOperator or
                                             OperationKind.Invocation or
                                             OperationKind.IsNull or
                                             OperationKind.IsPattern);
                                RoslynDebug.Assert(pointsToAnalysisResult != null);
                                RoslynDebug.Assert(valueContentAnalysisResult != null);

                                if (operation is IBinaryOperation binaryOperation &&
                                    binaryOperation.IsComparisonOperator() ||
                                    operation.Type?.SpecialType == SpecialType.System_Boolean)
                                {
                                    PredicateValueKind predicateKind = pointsToAnalysisResult.GetPredicateKind(operation);
                                    if (predicateKind != PredicateValueKind.Unknown)
                                    {
                                        return predicateKind;
                                    }

                                    if (copyAnalysisResultOpt != null)
                                    {
                                        predicateKind = copyAnalysisResultOpt.GetPredicateKind(operation);
                                        if (predicateKind != PredicateValueKind.Unknown)
                                        {
                                            return predicateKind;
                                        }
                                    }

                                    predicateKind = valueContentAnalysisResult.GetPredicateKind(operation);
                                    if (predicateKind != PredicateValueKind.Unknown)
                                    {
                                        return predicateKind;
                                    }
                                }

                                return PredicateValueKind.Unknown;
                            }

                            void ReportAlwaysTrueFalseOrNullDiagnostic(IOperation operation, PredicateValueKind predicateKind)
                            {
                                Debug.Assert(predicateKind != PredicateValueKind.Unknown);

                                // '{0}' is always '{1}'. Remove or refactor the condition(s) to avoid dead code.
                                var arg1 = operation.Syntax.ToString();
                                var arg2 = predicateKind == PredicateValueKind.AlwaysTrue ?
                                    (operation.Language == LanguageNames.VisualBasic ? "True" : "true") :
                                    (operation.Language == LanguageNames.VisualBasic ? "False" : "false");
                                var diagnostic = operation.CreateDiagnostic(AlwaysTrueFalseOrNullRule, arg1, arg2);
                                operationBlockContext.ReportDiagnostic(diagnostic);
                            }
                        }
                    }
                });
            });
        }
    }
}
