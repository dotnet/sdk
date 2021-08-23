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
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseSecureCookiesASPNetCore : DiagnosticAnalyzer
    {
        internal static DiagnosticDescriptor DefinitelyUseSecureCookiesASPNetCoreRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5382",
            typeof(MicrosoftNetCoreAnalyzersResources),
            nameof(MicrosoftNetCoreAnalyzersResources.DefinitelyUseSecureCookiesASPNetCore),
            nameof(MicrosoftNetCoreAnalyzersResources.DefinitelyUseSecureCookiesASPNetCoreMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: true,
            isReportedAtCompilationEnd: true,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.UseSecureCookiesASPNetCoreDescription));
        internal static DiagnosticDescriptor MaybeUseSecureCookiesASPNetCoreRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5383",
            typeof(MicrosoftNetCoreAnalyzersResources),
            nameof(MicrosoftNetCoreAnalyzersResources.MaybeUseSecureCookiesASPNetCore),
            nameof(MicrosoftNetCoreAnalyzersResources.MaybeUseSecureCookiesASPNetCoreMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: true,
            isReportedAtCompilationEnd: true,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.UseSecureCookiesASPNetCoreDescription));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
                                                                                        DefinitelyUseSecureCookiesASPNetCoreRule,
                                                                                        MaybeUseSecureCookiesASPNetCoreRule);

        private static readonly ConstructorMapper constructorMapper = new(
                                                                        ImmutableArray.Create<PropertySetAbstractValueKind>(
                                                                            PropertySetAbstractValueKind.Flagged));

        private static readonly PropertyMapperCollection PropertyMappers = new(
            new PropertyMapper(
                "Secure",
                (ValueContentAbstractValue valueContentAbstractValue) =>
                {
                    return PropertySetCallbacks.EvaluateLiteralValues(valueContentAbstractValue, o => o != null && o.Equals(false));
                }));

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

            // If there are more classes implement IResponseCookies, add them here later.
            HazardousUsageEvaluatorCollection hazardousUsageEvaluators = new HazardousUsageEvaluatorCollection(
                new HazardousUsageEvaluator(
                    WellKnownTypeNames.MicrosoftAspNetCoreHttpInternalResponseCookies,
                    "Append",
                    "options",
                    HazardousUsageCallback));

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);

                    if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                            WellKnownTypeNames.MicrosoftAspNetCoreHttpIResponseCookies,
                            out var iResponseCookiesTypeSymbol))
                    {
                        return;
                    }

                    wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.MicrosoftAspNetCoreHttpCookieOptions,
                        out var cookieOptionsTypeSymbol);
                    var rootOperationsNeedingAnalysis = PooledHashSet<(IOperation, ISymbol)>.GetInstance();

                    compilationStartAnalysisContext.RegisterOperationBlockStartAction(
                        (OperationBlockStartAnalysisContext operationBlockStartAnalysisContext) =>
                        {
                            // TODO: Handle case when exactly one of the below rules is configured to skip analysis.
                            if (operationBlockStartAnalysisContext.Options.IsConfiguredToSkipAnalysis(DefinitelyUseSecureCookiesASPNetCoreRule,
                                    operationBlockStartAnalysisContext.OwningSymbol, operationBlockStartAnalysisContext.Compilation) &&
                                operationBlockStartAnalysisContext.Options.IsConfiguredToSkipAnalysis(MaybeUseSecureCookiesASPNetCoreRule,
                                    operationBlockStartAnalysisContext.OwningSymbol, operationBlockStartAnalysisContext.Compilation))
                            {
                                return;
                            }

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    var invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;
                                    var methodSymbol = invocationOperation.TargetMethod;

                                    if (methodSymbol.ContainingType is INamedTypeSymbol namedTypeSymbol &&
                                        namedTypeSymbol.Interfaces.Contains(iResponseCookiesTypeSymbol) &&
                                        invocationOperation.TargetMethod.Name == "Append")
                                    {
                                        if (methodSymbol.Parameters.Length < 3)
                                        {
                                            operationAnalysisContext.ReportDiagnostic(
                                                invocationOperation.CreateDiagnostic(
                                                    DefinitelyUseSecureCookiesASPNetCoreRule));
                                        }
                                        else
                                        {
                                            lock (rootOperationsNeedingAnalysis)
                                            {
                                                rootOperationsNeedingAnalysis.Add((invocationOperation.GetRoot(), operationAnalysisContext.ContainingSymbol));
                                            }
                                        }
                                    }
                                },
                                OperationKind.Invocation);

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    var argumentOperation = (IArgumentOperation)operationAnalysisContext.Operation;

                                    if (argumentOperation.Parameter.Type.Equals(cookieOptionsTypeSymbol))
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
                                        WellKnownTypeNames.MicrosoftAspNetCoreHttpCookieOptions,
                                        constructorMapper,
                                        PropertyMappers,
                                        hazardousUsageEvaluators,
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
                                            descriptor = DefinitelyUseSecureCookiesASPNetCoreRule;
                                            break;

                                        case HazardousUsageEvaluationResult.MaybeFlagged:
                                            descriptor = MaybeUseSecureCookiesASPNetCoreRule;
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
