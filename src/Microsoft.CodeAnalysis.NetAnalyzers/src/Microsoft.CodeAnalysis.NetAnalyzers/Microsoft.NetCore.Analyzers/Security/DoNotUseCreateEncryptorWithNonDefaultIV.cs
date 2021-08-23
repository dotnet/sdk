// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseCreateEncryptorWithNonDefaultIV : DiagnosticAnalyzer
    {
        internal static DiagnosticDescriptor DefinitelyUseCreateEncryptorWithNonDefaultIVRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5401",
            nameof(MicrosoftNetCoreAnalyzersResources.DefinitelyUseCreateEncryptorWithNonDefaultIV),
            nameof(MicrosoftNetCoreAnalyzersResources.DefinitelyUseCreateEncryptorWithNonDefaultIVMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: true,
            isReportedAtCompilationEnd: true,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseCreateEncryptorWithNonDefaultIVDescription));
        internal static DiagnosticDescriptor MaybeUseCreateEncryptorWithNonDefaultIVRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5402",
            nameof(MicrosoftNetCoreAnalyzersResources.MaybeUseCreateEncryptorWithNonDefaultIV),
            nameof(MicrosoftNetCoreAnalyzersResources.MaybeUseCreateEncryptorWithNonDefaultIVMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: true,
            isReportedAtCompilationEnd: true,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseCreateEncryptorWithNonDefaultIVDescription));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
                                                                                        DefinitelyUseCreateEncryptorWithNonDefaultIVRule,
                                                                                        MaybeUseCreateEncryptorWithNonDefaultIVRule);

        private static readonly ConstructorMapper ConstructorMapper = new(
            (IMethodSymbol constructorMethod, IReadOnlyList<PointsToAbstractValue> argumentPointsToAbstractValues) =>
            {
                return PropertySetAbstractValue.GetInstance(PropertySetAbstractValueKind.Unflagged);
            });

        private static readonly PropertyMapperCollection PropertyMappers = new(
            new PropertyMapper(
                "IV",
                (PointsToAbstractValue pointsToAbstractValue) =>
                {
                    return PropertySetAbstractValueKind.Flagged;
                }));

        private static readonly HazardousUsageEvaluatorCollection HazardousUsageEvaluators = new(
            new HazardousUsageEvaluator(
                "CreateEncryptor",
                (IMethodSymbol methodSymbol, PropertySetAbstractValue abstractValue) =>
                {
                    // The passed rgbIV can't be null cause CreateEncryptor method has an ANE exception.
                    // It definitely uses a non-default IV and will be flagged a diagnostic directly without PropertySetAnalysis.
                    // So, it returns Unflagged to avoid repeated diagnostic.
                    if (!methodSymbol.Parameters.IsEmpty)
                    {
                        return HazardousUsageEvaluationResult.Unflagged;
                    }
                    else //Only look into the case using CreateEncryptor() and see if the property IV is set manually.
                    {
                        return PropertySetCallbacks.HazardousIfAllFlaggedAndAtLeastOneKnown(abstractValue);
                    }
                })
            );

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);

                    if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographySymmetricAlgorithm, out var symmetricAlgorithmTypeSymbol))
                    {
                        return;
                    }

                    var rootOperationsNeedingAnalysis = PooledHashSet<(IOperation, ISymbol)>.GetInstance();

                    compilationStartAnalysisContext.RegisterOperationBlockStartAction(
                        (OperationBlockStartAnalysisContext operationBlockStartAnalysisContext) =>
                        {
                            var owningSymbol = operationBlockStartAnalysisContext.OwningSymbol;

                            // TODO: Handle case when exactly one of the below rules is configured to skip analysis.
                            if (operationBlockStartAnalysisContext.Options.IsConfiguredToSkipAnalysis(DefinitelyUseCreateEncryptorWithNonDefaultIVRule,
                                    owningSymbol, operationBlockStartAnalysisContext.Compilation) &&
                                operationBlockStartAnalysisContext.Options.IsConfiguredToSkipAnalysis(MaybeUseCreateEncryptorWithNonDefaultIVRule,
                                    owningSymbol, operationBlockStartAnalysisContext.Compilation))
                            {
                                return;
                            }

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    var invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;
                                    var methodSymbol = invocationOperation.TargetMethod;

                                    if (methodSymbol.ContainingType.GetBaseTypesAndThis().Contains(symmetricAlgorithmTypeSymbol) &&
                                        methodSymbol.Name == "CreateEncryptor")
                                    {
                                        if (methodSymbol.Parameters.IsEmpty)
                                        {
                                            lock (rootOperationsNeedingAnalysis)
                                            {
                                                rootOperationsNeedingAnalysis.Add((invocationOperation.GetRoot(), operationAnalysisContext.ContainingSymbol));
                                            }
                                        }
                                        else
                                        {
                                            operationAnalysisContext.ReportDiagnostic(
                                                invocationOperation.CreateDiagnostic(
                                                    DefinitelyUseCreateEncryptorWithNonDefaultIVRule));
                                        }
                                    }
                                },
                                OperationKind.Invocation);
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
                                        WellKnownTypeNames.SystemSecurityCryptographySymmetricAlgorithm,
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
                                            descriptor = DefinitelyUseCreateEncryptorWithNonDefaultIVRule;
                                            break;

                                        case HazardousUsageEvaluationResult.MaybeFlagged:
                                            descriptor = MaybeUseCreateEncryptorWithNonDefaultIVRule;
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
