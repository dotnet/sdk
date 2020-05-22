using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.FlowAnalysis.Analysis.PropertySetAnalysis;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DataSetAnalyzer : DiagnosticAnalyzer
    {
        internal DiagnosticDescriptor DefinitelyNoReadXmlSchemaDescriptor = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA2351",
            nameof(MicrosoftNetCoreAnalyzersResources.DataSetDefinitelyInsecureTitle),
            nameof(MicrosoftNetCoreAnalyzersResources.DataSetDefinitelyInsecureMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: true);
        internal DiagnosticDescriptor MaybeNoReadXmlSchemaDescriptor = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA2352",
            nameof(MicrosoftNetCoreAnalyzersResources.DataSetDefinitelyInsecureTitle),
            nameof(MicrosoftNetCoreAnalyzersResources.DataSetDefinitelyInsecureMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DefinitelyNoReadXmlSchemaDescriptor, MaybeNoReadXmlSchemaDescriptor);

        private static readonly PropertyMapperCollection PropertyMappers =
            new PropertyMapperCollection(
                new PropertyMapper("...dummy", PropertySetCallbacks.AlwaysUnknown));
        private static readonly ConstructorMapper ConstructorMapper =
            new ConstructorMapper(PropertySetAbstractValueKind.Flagged);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    if (!compilationStartAnalysisContext.Compilation.TryGetOrCreateTypeByMetadataName(
                            WellKnownTypeNames.SystemDataDataSet,
                            out INamedTypeSymbol? dataSetTypeSymbol))
                    {
                        return;
                    }

                    PooledHashSet<(IOperation Operation, ISymbol ContainingSymbol)> rootOperationsNeedingAnalysis = PooledHashSet<(IOperation, ISymbol)>.GetInstance();

                    compilationStartAnalysisContext.RegisterOperationBlockStartAction(
                        (OperationBlockStartAnalysisContext operationBlockStartAnalysisContext) =>
                        {
                            ISymbol owningSymbol = operationBlockStartAnalysisContext.OwningSymbol;

                            // TODO: Handle case when exactly one of the below rules is configured to skip analysis.
                            if (owningSymbol.IsConfiguredToSkipAnalysis(
                                    operationBlockStartAnalysisContext.Options,
                                    DefinitelyNoReadXmlSchemaDescriptor,
                                    operationBlockStartAnalysisContext.Compilation,
                                    operationBlockStartAnalysisContext.CancellationToken)
                                && owningSymbol.IsConfiguredToSkipAnalysis(
                                    operationBlockStartAnalysisContext.Options,
                                    MaybeNoReadXmlSchemaDescriptor,
                                    operationBlockStartAnalysisContext.Compilation,
                                    operationBlockStartAnalysisContext.CancellationToken))
                            {
                                return;
                            }

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    IObjectCreationOperation creationOperation =
                                        (IObjectCreationOperation)operationAnalysisContext.Operation;
                                    if (creationOperation.Type?.DerivesFrom(dataSetTypeSymbol) == true)
                                    {
                                        lock (rootOperationsNeedingAnalysis)
                                        {
                                            rootOperationsNeedingAnalysis.Add(
                                                (operationAnalysisContext.Operation.GetRoot(),
                                                operationAnalysisContext.ContainingSymbol));
                                        }
                                    }
                                },
                                OperationKind.ObjectCreation);

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    IInvocationOperation invocationOperation =
                                        (IInvocationOperation)operationAnalysisContext.Operation;
                                    if (invocationOperation.Type?.DerivesFrom(dataSetTypeSymbol) == true
                                        && invocationOperation.TargetMethod.Name == "ReadXml")
                                    {
                                        lock (rootOperationsNeedingAnalysis)
                                        {
                                            rootOperationsNeedingAnalysis.Add(
                                                (operationAnalysisContext.Operation.GetRoot(),
                                                operationAnalysisContext.ContainingSymbol));
                                        }
                                    }
                                },
                                OperationKind.Invocation);

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    IMethodReferenceOperation methodReferenceOperation =
                                        (IMethodReferenceOperation)operationAnalysisContext.Operation;
                                    if (methodReferenceOperation.Instance?.Type.DerivesFrom(dataSetTypeSymbol) == true
                                       && methodReferenceOperation.Method.MetadataName == "ReadXml")
                                    {
                                        lock (rootOperationsNeedingAnalysis)
                                        {
                                            rootOperationsNeedingAnalysis.Add(
                                                (operationAnalysisContext.Operation.GetRoot(),
                                                operationAnalysisContext.ContainingSymbol));
                                        }
                                    }
                                },
                                OperationKind.MethodReference);
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
                                        this.DeserializerTypeMetadataName,
                                        DoNotUseInsecureDeserializerWithoutBinderBase.ConstructorMapper,
                                        propertyMappers,
                                        InvocationMapperCollection.Empty,
                                        hazardousUsageEvaluators,
                                        InterproceduralAnalysisConfiguration.Create(
                                            compilationAnalysisContext.Options,
                                            SupportedDiagnostics,
                                            defaultInterproceduralAnalysisKind: InterproceduralAnalysisKind.ContextSensitive,
                                            cancellationToken: compilationAnalysisContext.CancellationToken));
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
                                            descriptor = this.BinderDefinitelyNotSetDescriptor!;
                                            break;

                                        case HazardousUsageEvaluationResult.MaybeFlagged:
                                            descriptor = this.BinderMaybeNotSetDescriptor!;
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
                                rootOperationsNeedingAnalysis.Free();
                                allResults?.Free();
                            }
                        });
                });

                    }
                }
}
