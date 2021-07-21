// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    /// <summary>
    /// For detecting deserialization with <see cref="T:System.Web.Script.Serialization.JavaScriptSerializer"/>.
    /// </summary>
    [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal class DoNotUseInsecureDeserializerJavaScriptSerializerWithSimpleTypeResolver : DiagnosticAnalyzer
    {
        internal static readonly DiagnosticDescriptor DefinitelyWithSimpleTypeResolver =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2321",
                nameof(MicrosoftNetCoreAnalyzersResources.JavaScriptSerializerWithSimpleTypeResolverTitle),
                nameof(MicrosoftNetCoreAnalyzersResources.JavaScriptSerializerWithSimpleTypeResolverMessage),
                RuleLevel.Disabled,
                isPortedFxCopRule: false,
                isDataflowRule: true,
                isReportedAtCompilationEnd: true);
        internal static readonly DiagnosticDescriptor MaybeWithSimpleTypeResolver =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2322",
                nameof(MicrosoftNetCoreAnalyzersResources.JavaScriptSerializerMaybeWithSimpleTypeResolverTitle),
                nameof(MicrosoftNetCoreAnalyzersResources.JavaScriptSerializerMaybeWithSimpleTypeResolverMessage),
                RuleLevel.Disabled,
                isPortedFxCopRule: false,
                isDataflowRule: true,
                isReportedAtCompilationEnd: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                DefinitelyWithSimpleTypeResolver,
                MaybeWithSimpleTypeResolver);

        private static readonly PropertyMapperCollection PropertyMappers = new(
            new PropertyMapper(
                "...dummy name",    // There isn't *really* a property for what we're tracking; just the constructor argument.
                (PointsToAbstractValue v) => PropertySetAbstractValueKind.Unknown));

        private static HazardousUsageEvaluationResult HazardousUsageCallback(IMethodSymbol methodSymbol, PropertySetAbstractValue propertySetAbstractValue)
        {
            return propertySetAbstractValue[0] switch
            {
                PropertySetAbstractValueKind.Flagged => HazardousUsageEvaluationResult.Flagged,
                PropertySetAbstractValueKind.Unflagged => HazardousUsageEvaluationResult.Unflagged,
                _ => HazardousUsageEvaluationResult.MaybeFlagged,
            };
        }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            HazardousUsageEvaluatorCollection hazardousUsageEvaluators = new HazardousUsageEvaluatorCollection(
                SecurityHelpers.JavaScriptSerializerDeserializationMethods.Select(
                    (string methodName) => new HazardousUsageEvaluator(methodName, HazardousUsageCallback)));

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    WellKnownTypeProvider wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);
                    if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebScriptSerializationJavaScriptSerializer, out INamedTypeSymbol? javaScriptSerializerSymbol)
                        || !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebScriptSerializationJavaScriptTypeResolver, out INamedTypeSymbol? javaScriptTypeResolverSymbol)
                        || !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebScriptSerializationSimpleTypeResolver, out INamedTypeSymbol? simpleTypeResolverSymbol))
                    {
                        return;
                    }

                    // If JavaScriptSerializer is initialized with a SimpleTypeResolver, then that instance is flagged.
                    ConstructorMapper constructorMapper = new ConstructorMapper(
                        (IMethodSymbol constructorMethod, IReadOnlyList<PointsToAbstractValue> argumentPointsToAbstractValues) =>
                        {
                            PropertySetAbstractValueKind kind;
                            if (constructorMethod.Parameters.IsEmpty)
                            {
                                kind = PropertySetAbstractValueKind.Unflagged;
                            }
                            else if (constructorMethod.Parameters.Length == 1
                                && javaScriptTypeResolverSymbol.Equals(constructorMethod.Parameters[0].Type))
                            {
                                PointsToAbstractValue pointsTo = argumentPointsToAbstractValues[0];
                                switch (pointsTo.Kind)
                                {
                                    case PointsToAbstractValueKind.Invalid:
                                    case PointsToAbstractValueKind.UnknownNull:
                                    case PointsToAbstractValueKind.Undefined:
                                        kind = PropertySetAbstractValueKind.Unflagged;
                                        break;

                                    case PointsToAbstractValueKind.KnownLocations:
                                        if (pointsTo.Locations.Any(l => !l.IsNull && simpleTypeResolverSymbol.Equals(l.LocationType)))
                                        {
                                            kind = PropertySetAbstractValueKind.Flagged;
                                        }
                                        else if (pointsTo.Locations.Any(l =>
                                                    !l.IsNull
                                                    && javaScriptTypeResolverSymbol.Equals(l.LocationType)
                                                    && (l.Creation == null || l.Creation.Kind != OperationKind.ObjectCreation)))
                                        {
                                            // Points to a JavaScriptTypeResolver, but we don't know if the instance is a SimpleTypeResolver.
                                            kind = PropertySetAbstractValueKind.MaybeFlagged;
                                        }
                                        else
                                        {
                                            kind = PropertySetAbstractValueKind.Unflagged;
                                        }

                                        break;

                                    case PointsToAbstractValueKind.UnknownNotNull:
                                    case PointsToAbstractValueKind.Unknown:
                                        kind = PropertySetAbstractValueKind.MaybeFlagged;
                                        break;

                                    default:
                                        Debug.Fail($"Unhandled PointsToAbstractValueKind {pointsTo.Kind}");
                                        kind = PropertySetAbstractValueKind.Unflagged;
                                        break;
                                }
                            }
                            else
                            {
                                Debug.Fail($"Unhandled JavaScriptSerializer constructor {constructorMethod.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}");
                                kind = PropertySetAbstractValueKind.Unflagged;
                            }

                            return PropertySetAbstractValue.GetInstance(kind);
                        });

                    PooledHashSet<(IOperation Operation, ISymbol ContainingSymbol)> rootOperationsNeedingAnalysis = PooledHashSet<(IOperation, ISymbol)>.GetInstance();

                    compilationStartAnalysisContext.RegisterOperationBlockStartAction(
                        (OperationBlockStartAnalysisContext operationBlockStartAnalysisContext) =>
                        {
                            var owningSymbol = operationBlockStartAnalysisContext.OwningSymbol;

                            // TODO: Handle case when exactly one of the below rules is configured to skip analysis.
                            if (operationBlockStartAnalysisContext.Options.IsConfiguredToSkipAnalysis(DefinitelyWithSimpleTypeResolver,
                                    owningSymbol, operationBlockStartAnalysisContext.Compilation) &&
                                operationBlockStartAnalysisContext.Options.IsConfiguredToSkipAnalysis(MaybeWithSimpleTypeResolver,
                                    owningSymbol, operationBlockStartAnalysisContext.Compilation))
                            {
                                return;
                            }

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    IInvocationOperation invocationOperation =
                                        (IInvocationOperation)operationAnalysisContext.Operation;
                                    if ((javaScriptSerializerSymbol.Equals(invocationOperation.Instance?.Type)
                                            && SecurityHelpers.JavaScriptSerializerDeserializationMethods.Contains(invocationOperation.TargetMethod.Name))
                                        || simpleTypeResolverSymbol.Equals(invocationOperation.TargetMethod.ReturnType)
                                        || javaScriptTypeResolverSymbol.Equals(invocationOperation.TargetMethod.ReturnType))
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
                                    IMethodReferenceOperation methodReferenceOperation =
                                        (IMethodReferenceOperation)operationAnalysisContext.Operation;
                                    if (javaScriptSerializerSymbol.Equals(methodReferenceOperation.Instance?.Type)
                                        && SecurityHelpers.JavaScriptSerializerDeserializationMethods.Contains(methodReferenceOperation.Method.Name))
                                    {
                                        lock (rootOperationsNeedingAnalysis)
                                        {
                                            rootOperationsNeedingAnalysis.Add((operationAnalysisContext.Operation.GetRoot(), operationAnalysisContext.ContainingSymbol));
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
                                        WellKnownTypeNames.SystemWebScriptSerializationJavaScriptSerializer,
                                        constructorMapper,
                                        PropertyMappers,
                                        hazardousUsageEvaluators,
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
                                    DiagnosticDescriptor descriptor;
                                    switch (kvp.Value)
                                    {
                                        case HazardousUsageEvaluationResult.Flagged:
                                            descriptor = DefinitelyWithSimpleTypeResolver;
                                            break;

                                        case HazardousUsageEvaluationResult.MaybeFlagged:
                                            descriptor = MaybeWithSimpleTypeResolver;
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
