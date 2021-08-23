// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
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
    internal class SetHttpOnlyForHttpCookie : DiagnosticAnalyzer
    {
        internal static DiagnosticDescriptor Rule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5396",
            typeof(MicrosoftNetCoreAnalyzersResources),
            nameof(MicrosoftNetCoreAnalyzersResources.SetHttpOnlyForHttpCookie),
            nameof(MicrosoftNetCoreAnalyzersResources.SetHttpOnlyForHttpCookieMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: true,
            isReportedAtCompilationEnd: true,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.SetHttpOnlyForHttpCookieDescription));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                Rule);

        private static readonly ConstructorMapper ConstructorMapper = new(
            (IMethodSymbol constructorMethod,
            IReadOnlyList<PointsToAbstractValue> argumentPointsToAbstractValues) =>
            {
                return PropertySetAbstractValue.GetInstance(PropertySetAbstractValueKind.Flagged);
            });

        // If HttpOnly is set explicitly, the callbacks of OperationKind.SimpleAssignment can cover that case.
        // Otherwise, using PropertySetAnalysis to cover the case where HttpCookie object is returned without initializing or assgining HttpOnly property.
        private static readonly PropertyMapperCollection PropertyMappers = new(
            new PropertyMapper(
                "HttpOnly",
                (PointsToAbstractValue pointsToAbstractValue) =>
                   PropertySetAbstractValueKind.Unflagged));

        private static readonly HazardousUsageEvaluatorCollection HazardousUsageEvaluators = new(
                    new HazardousUsageEvaluator(
                        HazardousUsageEvaluatorKind.Return,
                        PropertySetCallbacks.HazardousIfAllFlaggedAndAtLeastOneKnown),
                    new HazardousUsageEvaluator(
                        HazardousUsageEvaluatorKind.Argument,
                        PropertySetCallbacks.HazardousIfAllFlaggedAndAtLeastOneKnown));

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    if (!compilationStartAnalysisContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebHttpCookie,
                            out INamedTypeSymbol? httpCookieSymbol))
                    {
                        return;
                    }

                    PooledHashSet<(IOperation Operation, ISymbol ContainingSymbol)> rootOperationsNeedingAnalysis = PooledHashSet<(IOperation, ISymbol)>.GetInstance();

                    compilationStartAnalysisContext.RegisterOperationBlockStartAction(
                        (OperationBlockStartAnalysisContext operationBlockStartAnalysisContext) =>
                        {
                            ISymbol owningSymbol = operationBlockStartAnalysisContext.OwningSymbol;

                            if (operationBlockStartAnalysisContext.Options.IsConfiguredToSkipAnalysis(Rule, owningSymbol, operationBlockStartAnalysisContext.Compilation))
                            {
                                return;
                            }

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    ISimpleAssignmentOperation simpleAssignmentOperation =
                                        (ISimpleAssignmentOperation)operationAnalysisContext.Operation;

                                    if (simpleAssignmentOperation.Target is IPropertyReferenceOperation propertyReferenceOperation &&
                                        httpCookieSymbol.Equals(propertyReferenceOperation.Property.ContainingType) &&
                                        propertyReferenceOperation.Property.Name == "HttpOnly" &&
                                        simpleAssignmentOperation.Value.ConstantValue.HasValue &&
                                        simpleAssignmentOperation.Value.ConstantValue.Value.Equals(false))
                                    {
                                        operationAnalysisContext.ReportDiagnostic(
                                            simpleAssignmentOperation.CreateDiagnostic(
                                                Rule));
                                    }
                                },
                                OperationKind.SimpleAssignment);

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    IReturnOperation returnOperation = (IReturnOperation)operationAnalysisContext.Operation;

                                    if (httpCookieSymbol.Equals(returnOperation.ReturnedValue?.Type))
                                    {
                                        lock (rootOperationsNeedingAnalysis)
                                        {
                                            rootOperationsNeedingAnalysis.Add(
                                                (returnOperation.GetRoot(), operationAnalysisContext.ContainingSymbol));
                                        }
                                    }
                                },
                                OperationKind.Return);

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    IArgumentOperation argumentOperation = (IArgumentOperation)operationAnalysisContext.Operation;

                                    if (httpCookieSymbol.Equals(argumentOperation.Value.Type))
                                    {
                                        lock (rootOperationsNeedingAnalysis)
                                        {
                                            rootOperationsNeedingAnalysis.Add(
                                                (argumentOperation.GetRoot(), operationAnalysisContext.ContainingSymbol));
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
                                        WellKnownTypeNames.SystemWebHttpCookie,
                                        ConstructorMapper,
                                        PropertyMappers,
                                        HazardousUsageEvaluators,
                                        InterproceduralAnalysisConfiguration.Create(
                                            compilationAnalysisContext.Options,
                                            SupportedDiagnostics,
                                            rootOperationsNeedingAnalysis.First().Operation,
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
                                    if (kvp.Value == HazardousUsageEvaluationResult.Flagged)
                                    {
                                        compilationAnalysisContext.ReportDiagnostic(
                                            Diagnostic.Create(
                                                Rule,
                                                kvp.Key.Location));
                                    }
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
