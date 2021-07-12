// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
    internal class DoNotDisableHttpClientCRLCheck : DiagnosticAnalyzer
    {
        internal static DiagnosticDescriptor DefinitelyDisableHttpClientCRLCheckRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5399",
            nameof(MicrosoftNetCoreAnalyzersResources.DefinitelyDisableHttpClientCRLCheck),
            nameof(MicrosoftNetCoreAnalyzersResources.DefinitelyDisableHttpClientCRLCheckMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: true,
            isReportedAtCompilationEnd: true,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.DoNotDisableHttpClientCRLCheckDescription));
        internal static DiagnosticDescriptor MaybeDisableHttpClientCRLCheckRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5400",
            nameof(MicrosoftNetCoreAnalyzersResources.MaybeDisableHttpClientCRLCheck),
            nameof(MicrosoftNetCoreAnalyzersResources.MaybeDisableHttpClientCRLCheckMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: true,
            isReportedAtCompilationEnd: true,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.DoNotDisableHttpClientCRLCheckDescription));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                DefinitelyDisableHttpClientCRLCheckRule,
                MaybeDisableHttpClientCRLCheckRule);

        /// <summary>
        /// PropertySetAbstractValue index for CheckCertificateRevocationList property.
        /// </summary>
        private const int CheckCertificateRevocationListIndex = 0;

        /// <summary>
        /// PropertySetAbstractValue index for ServerCertificateValidationCallback property.
        /// </summary>
        private const int ServerCertificateValidationCallbackIndex = 1;

        private static readonly ConstructorMapper ConstructorMapper = new(
            (IMethodSymbol constructorMethod, IReadOnlyList<PointsToAbstractValue> argumentPointsToAbstractValues) =>
            {
                return PropertySetAbstractValue.GetInstance(PropertySetAbstractValueKind.Flagged, PropertySetAbstractValueKind.Flagged);
            });

        private static readonly PropertyMapperCollection PropertyMappers = new(
            new PropertyMapper(
                "CheckCertificateRevocationList",
                (ValueContentAbstractValue valueContentAbstractValue) =>
                    PropertySetCallbacks.EvaluateLiteralValues(
                        valueContentAbstractValue,
                        (object? o) => o is false),
                CheckCertificateRevocationListIndex),
            new PropertyMapper(
                "ServerCertificateCustomValidationCallback",
                PropertySetCallbacks.FlagIfNull,
                ServerCertificateValidationCallbackIndex),
            new PropertyMapper(
                "ServerCertificateValidationCallback",
                PropertySetCallbacks.FlagIfNull,
                ServerCertificateValidationCallbackIndex));

        private static readonly HazardousUsageEvaluatorCollection HazardousUsageEvaluators = new(
            new HazardousUsageEvaluator(
                WellKnownTypeNames.SystemNetHttpHttpClient,
                ".ctor",
                "handler",
                (IMethodSymbol methodSymbol, PropertySetAbstractValue abstractValue) =>
                {
                    return abstractValue[ServerCertificateValidationCallbackIndex] switch
                    {
                        PropertySetAbstractValueKind.Flagged => abstractValue[CheckCertificateRevocationListIndex] switch
                        {
                            PropertySetAbstractValueKind.Flagged => HazardousUsageEvaluationResult.Flagged,
                            PropertySetAbstractValueKind.MaybeFlagged => HazardousUsageEvaluationResult.MaybeFlagged,
                            _ => HazardousUsageEvaluationResult.Unflagged,
                        },

                        PropertySetAbstractValueKind.MaybeFlagged => abstractValue[CheckCertificateRevocationListIndex] switch
                        {
                            PropertySetAbstractValueKind.Unflagged => HazardousUsageEvaluationResult.Unflagged,
                            _ => HazardousUsageEvaluationResult.MaybeFlagged,
                        },

                        _ => HazardousUsageEvaluationResult.Unflagged,
                    };
                },
                true));

        private static readonly ImmutableHashSet<string> typeToTrackMetadataNames = ImmutableHashSet.Create<string>(
            WellKnownTypeNames.SystemNetHttpWinHttpHandler,
            WellKnownTypeNames.SystemNetHttpHttpClientHandler);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);

                    if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNetHttpHttpClient, out INamedTypeSymbol? httpClientTypeSymbol))
                    {
                        return;
                    }

                    if (typeToTrackMetadataNames.All(s => !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(s, out _)))
                    {
                        return;
                    }

                    var rootOperationsNeedingAnalysis = PooledHashSet<(IOperation, ISymbol)>.GetInstance();

                    compilationStartAnalysisContext.RegisterOperationBlockStartAction(
                        (OperationBlockStartAnalysisContext operationBlockStartAnalysisContext) =>
                        {
                            ISymbol owningSymbol = operationBlockStartAnalysisContext.OwningSymbol;

                            if (operationBlockStartAnalysisContext.Options.IsConfiguredToSkipAnalysis(
                                    DefinitelyDisableHttpClientCRLCheckRule,
                                    owningSymbol,
                                    operationBlockStartAnalysisContext.Compilation) &&
                                operationBlockStartAnalysisContext.Options.IsConfiguredToSkipAnalysis(
                                    MaybeDisableHttpClientCRLCheckRule,
                                    owningSymbol,
                                    operationBlockStartAnalysisContext.Compilation))
                            {
                                return;
                            }

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    var objectCreationOperation =
                                        (IObjectCreationOperation)operationAnalysisContext.Operation;

                                    if (objectCreationOperation.Type.GetBaseTypesAndThis().Contains(httpClientTypeSymbol))
                                    {
                                        if (!objectCreationOperation.Arguments.IsEmpty)
                                        {
                                            lock (rootOperationsNeedingAnalysis)
                                            {
                                                rootOperationsNeedingAnalysis.Add(
                                                    (objectCreationOperation.GetRoot(), operationAnalysisContext.ContainingSymbol));
                                            }
                                        }
                                    }
                                },
                                OperationKind.ObjectCreation);
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
                                        typeToTrackMetadataNames,
                                        ConstructorMapper,
                                        PropertyMappers,
                                        HazardousUsageEvaluators,
                                        InterproceduralAnalysisConfiguration.Create(
                                            compilationAnalysisContext.Options,
                                            SupportedDiagnostics,
                                            rootOperationsNeedingAnalysis.First().Item1,
                                            compilationAnalysisContext.Compilation,
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
                                            descriptor = DefinitelyDisableHttpClientCRLCheckRule;
                                            break;

                                        case HazardousUsageEvaluationResult.MaybeFlagged:
                                            descriptor = MaybeDisableHttpClientCRLCheckRule;
                                            break;

                                        default:
                                            Debug.Fail($"Unhandled result value {kvp.Value}");
                                            continue;
                                    }

                                    RoslynDebug.Assert(kvp.Key.Method != null);    // HazardousUsageEvaluations only for invocations.
                                    compilationAnalysisContext.ReportDiagnostic(
                                        Diagnostic.Create(
                                            descriptor,
                                            kvp.Key.Location,
                                            kvp.Key.Method.ToDisplayString(
                                                SymbolDisplayFormat.MinimallyQualifiedFormat)));
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
