// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.FlowAnalysis.Analysis.PropertySetAnalysis;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseWeakKDFInsufficientIterationCount : DiagnosticAnalyzer
    {
        internal static DiagnosticDescriptor DefinitelyUseWeakKDFInsufficientIterationCountRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5387",
            typeof(MicrosoftNetCoreAnalyzersResources),
            nameof(MicrosoftNetCoreAnalyzersResources.DefinitelyUseWeakKDFInsufficientIterationCount),
            nameof(MicrosoftNetCoreAnalyzersResources.DefinitelyUseWeakKDFInsufficientIterationCountMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: true,
            isReportedAtCompilationEnd: true,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseWeakKDFInsufficientIterationCountDescription));
        internal static DiagnosticDescriptor MaybeUseWeakKDFInsufficientIterationCountRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5388",
            typeof(MicrosoftNetCoreAnalyzersResources),
            nameof(MicrosoftNetCoreAnalyzersResources.MaybeUseWeakKDFInsufficientIterationCount),
            nameof(MicrosoftNetCoreAnalyzersResources.MaybeUseWeakKDFInsufficientIterationCountMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: true,
            isReportedAtCompilationEnd: true,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseWeakKDFInsufficientIterationCountDescription));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
                                                                                        DefinitelyUseWeakKDFInsufficientIterationCountRule,
                                                                                        MaybeUseWeakKDFInsufficientIterationCountRule);

        private const int DefaultIterationCount = 1000;

        private static HazardousUsageEvaluationResult HazardousUsageCallback(IMethodSymbol methodSymbol, PropertySetAbstractValue propertySetAbstractValue)
        {
            return propertySetAbstractValue[0] switch
            {
                PropertySetAbstractValueKind.Flagged => HazardousUsageEvaluationResult.Flagged,

                PropertySetAbstractValueKind.MaybeFlagged => HazardousUsageEvaluationResult.MaybeFlagged,

                _ => HazardousUsageEvaluationResult.Unflagged,
            };
        }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            HazardousUsageEvaluatorCollection hazardousUsageEvaluators = new HazardousUsageEvaluatorCollection(
                new HazardousUsageEvaluator("GetBytes", HazardousUsageCallback));

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    if (!compilationStartAnalysisContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographyRfc2898DeriveBytes,
                            out var rfc2898DeriveBytesTypeSymbol) ||
                        compilationStartAnalysisContext.Compilation.SyntaxTrees.FirstOrDefault() is not SyntaxTree tree)
                    {
                        return;
                    }

                    var sufficientIterationCount = compilationStartAnalysisContext.Options.GetUnsignedIntegralOptionValue(
                        optionName: EditorConfigOptionNames.SufficientIterationCountForWeakKDFAlgorithm,
                        rule: DefinitelyUseWeakKDFInsufficientIterationCountRule,
                        tree,
                        compilationStartAnalysisContext.Compilation,
                        defaultValue: 100000);
                    var constructorMapper = new ConstructorMapper(
                        (IMethodSymbol constructorMethod, IReadOnlyList<ValueContentAbstractValue> argumentValueContentAbstractValues,
                        IReadOnlyList<PointsToAbstractValue> argumentPointsToAbstractValues) =>
                        {
                            var kind = DefaultIterationCount >= sufficientIterationCount ? PropertySetAbstractValueKind.Unflagged : PropertySetAbstractValueKind.Flagged;

                            if (constructorMethod.Parameters.Length >= 3)
                            {
                                if (constructorMethod.Parameters[2].Name == "iterations" &&
                                    constructorMethod.Parameters[2].Type.SpecialType == SpecialType.System_Int32)
                                {
                                    kind = PropertySetCallbacks.EvaluateLiteralValues(argumentValueContentAbstractValues[2], o => Convert.ToInt32(o, CultureInfo.InvariantCulture) < sufficientIterationCount);
                                }
                            }

                            return PropertySetAbstractValue.GetInstance(kind);
                        });
                    var propertyMappers = new PropertyMapperCollection(
                        new PropertyMapper(
                            "IterationCount",
                            (ValueContentAbstractValue valueContentAbstractValue) =>
                            {
                                return PropertySetCallbacks.EvaluateLiteralValues(valueContentAbstractValue, o => Convert.ToInt32(o, CultureInfo.InvariantCulture) < sufficientIterationCount);
                            }));
                    var rootOperationsNeedingAnalysis = PooledHashSet<(IOperation, ISymbol)>.GetInstance();

                    compilationStartAnalysisContext.RegisterOperationBlockStartAction(
                        (OperationBlockStartAnalysisContext operationBlockStartAnalysisContext) =>
                        {
                            // TODO: Handle case when exactly one of the below rules is configured to skip analysis.
                            if (operationBlockStartAnalysisContext.Options.IsConfiguredToSkipAnalysis(DefinitelyUseWeakKDFInsufficientIterationCountRule,
                                    operationBlockStartAnalysisContext.OwningSymbol, operationBlockStartAnalysisContext.Compilation) &&
                                operationBlockStartAnalysisContext.Options.IsConfiguredToSkipAnalysis(MaybeUseWeakKDFInsufficientIterationCountRule,
                                    operationBlockStartAnalysisContext.OwningSymbol, operationBlockStartAnalysisContext.Compilation))
                            {
                                return;
                            }

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    var invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;

                                    if (rfc2898DeriveBytesTypeSymbol.Equals(invocationOperation.Instance?.Type) &&
                                        invocationOperation.TargetMethod.Name == "GetBytes")
                                    {
                                        lock (rootOperationsNeedingAnalysis)
                                        {
                                            rootOperationsNeedingAnalysis.Add((invocationOperation.GetRoot(), operationAnalysisContext.ContainingSymbol));
                                        }
                                    }
                                },
                            OperationKind.Invocation);

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    var argumentOperation = (IArgumentOperation)operationAnalysisContext.Operation;

                                    if (rfc2898DeriveBytesTypeSymbol.Equals(argumentOperation.Parameter.Type))
                                    {
                                        lock (rootOperationsNeedingAnalysis)
                                        {
                                            rootOperationsNeedingAnalysis.Add((argumentOperation.GetRoot(), operationAnalysisContext.ContainingSymbol));
                                        }
                                    }
                                },
                                OperationKind.Argument);
                        });

                    compilationStartAnalysisContext.RegisterCompilationEndAction(
                        (CompilationAnalysisContext compilationAnalysisContext) =>
                        {
                            PooledDictionary<(Location Location, IMethodSymbol? Method), HazardousUsageEvaluationResult>? allResults = null;

                            try
                            {
                                lock (rootOperationsNeedingAnalysis)
                                {
                                    if (!rootOperationsNeedingAnalysis.Any())
                                    {
                                        return;
                                    }

                                    allResults = PropertySetAnalysis.BatchGetOrComputeHazardousUsages(
                                        compilationAnalysisContext.Compilation,
                                        rootOperationsNeedingAnalysis,
                                        compilationAnalysisContext.Options,
                                        WellKnownTypeNames.SystemSecurityCryptographyRfc2898DeriveBytes,
                                        constructorMapper,
                                        propertyMappers,
                                        hazardousUsageEvaluators,
                                        InterproceduralAnalysisConfiguration.Create(
                                            compilationAnalysisContext.Options,
                                            SupportedDiagnostics,
                                            rootOperationsNeedingAnalysis.First().Item1,
                                            compilationStartAnalysisContext.Compilation,
                                            defaultInterproceduralAnalysisKind: InterproceduralAnalysisKind.ContextSensitive));
                                }

                                if (allResults == null)
                                {
                                    return;
                                }

                                foreach (KeyValuePair<(Location Location, IMethodSymbol? Method), HazardousUsageEvaluationResult> kvp
                                    in allResults)
                                {
                                    DiagnosticDescriptor descriptor;
                                    switch (kvp.Value)
                                    {
                                        case HazardousUsageEvaluationResult.Flagged:
                                            descriptor = DefinitelyUseWeakKDFInsufficientIterationCountRule;
                                            break;

                                        case HazardousUsageEvaluationResult.MaybeFlagged:
                                            descriptor = MaybeUseWeakKDFInsufficientIterationCountRule;
                                            break;

                                        default:
                                            Debug.Fail($"Unhandled result value {kvp.Value}");
                                            continue;
                                    }

                                    compilationAnalysisContext.ReportDiagnostic(
                                        Diagnostic.Create(
                                            descriptor,
                                            kvp.Key.Location,
                                            sufficientIterationCount));
                                }
                            }
                            finally
                            {
                                rootOperationsNeedingAnalysis.Free(compilationAnalysisContext.CancellationToken);
                                allResults?.Free(compilationAnalysisContext.CancellationToken);
                            }
                        });

                });
        }
    }
}
