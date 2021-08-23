// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    /// <summary>
    /// For detecting potentially insecure deserialization settings with <see cref="T:Newtonsoft.Json.JsonSerializerSettings"/>.
    /// </summary>
    [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseInsecureSettingsForJsonNet : DiagnosticAnalyzer
    {
        internal static readonly DiagnosticDescriptor DefinitelyInsecureSettings =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2327",
                nameof(MicrosoftNetCoreAnalyzersResources.JsonNetInsecureSettingsTitle),
                nameof(MicrosoftNetCoreAnalyzersResources.JsonNetInsecureSettingsMessage),
                RuleLevel.Disabled,
                isPortedFxCopRule: false,
                isDataflowRule: true,
                isReportedAtCompilationEnd: true);
        internal static readonly DiagnosticDescriptor MaybeInsecureSettings =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2328",
                nameof(MicrosoftNetCoreAnalyzersResources.JsonNetMaybeInsecureSettingsTitle),
                nameof(MicrosoftNetCoreAnalyzersResources.JsonNetMaybeInsecureSettingsMessage),
                RuleLevel.Disabled,
                isPortedFxCopRule: false,
                isDataflowRule: true,
                isReportedAtCompilationEnd: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                DefinitelyInsecureSettings,
                MaybeInsecureSettings);

        /// <summary>
        /// PropertySetAbstractValue index for TypeNameHandling property.
        /// </summary>
        private const int TypeNameHandlingIndex = 0;

        /// <summary>
        /// PropertySetAbstractValue index for Binder / SerializationBinder properties (both are aliased to same underlying value).
        /// </summary>
        private const int SerializationBinderIndex = 1;

        private static readonly ConstructorMapper ConstructorMapper = new(
            (IMethodSymbol constructorMethod, IReadOnlyList<PointsToAbstractValue> argumentPointsToAbstractValues) =>
            {
                if (constructorMethod.Parameters.IsEmpty)
                {
                    return PropertySetAbstractValue.GetInstance(
                        PropertySetAbstractValueKind.Unflagged,   // TypeNameHandling defaults to None.
                        PropertySetAbstractValueKind.Flagged);    // Binder / SerializationBinder defaults to null.
                }
                else
                {
                    Debug.Fail($"Unhandled JsonSerializerSettings constructor {constructorMethod.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}");
                    return PropertySetAbstractValue.GetInstance(
                        PropertySetAbstractValueKind.Unflagged,
                        PropertySetAbstractValueKind.Unflagged);
                }
            });

        private static readonly PropertyMapperCollection PropertyMappers = new(
            new PropertyMapper(
                "TypeNameHandling",
                (ValueContentAbstractValue valueContentAbstractValue) =>
                    PropertySetCallbacks.EvaluateLiteralValues(
                        valueContentAbstractValue,
                        (object? o) => o is int i && i != 0),    // None is 0, and anything other than None is flagged.
                TypeNameHandlingIndex),
            new PropertyMapper(
                "Binder",
                PropertySetCallbacks.FlagIfNull,
                SerializationBinderIndex),      // Binder & SerializationBinder have the same underlying value.
            new PropertyMapper(
                "SerializationBinder",
                PropertySetCallbacks.FlagIfNull,
                SerializationBinderIndex));     // Binder & SerializationBinder have the same underlying value.

        private static readonly HazardousUsageEvaluatorCollection HazardousUsageEvaluators = new(
            SecurityHelpers.JsonSerializerInstantiateWithSettingsMethods.Select(
                (string methodName) => new HazardousUsageEvaluator(
                    WellKnownTypeNames.NewtonsoftJsonJsonSerializer,
                    methodName,
                    "settings",
                    PropertySetCallbacks.HazardousIfAllFlaggedAndAtLeastOneKnown))
                .Concat(
                    SecurityHelpers.JsonConvertWithSettingsMethods.Select(
                        (string methodName) => new HazardousUsageEvaluator(
                            WellKnownTypeNames.NewtonsoftJsonJsonConvert,
                            methodName,
                            "settings",
                            PropertySetCallbacks.HazardousIfAllFlaggedAndAtLeastOneKnown)))
                .Concat(
                    new HazardousUsageEvaluator(
                        HazardousUsageEvaluatorKind.Return,
                        PropertySetCallbacks.HazardousIfAllFlaggedAndAtLeastOneKnown))
                .Concat(
                    new HazardousUsageEvaluator(
                        HazardousUsageEvaluatorKind.Initialization,
                        PropertySetCallbacks.HazardousIfAllFlaggedAndAtLeastOneKnown)));

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    WellKnownTypeProvider wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);
                    if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.NewtonsoftJsonJsonSerializerSettings, out INamedTypeSymbol? jsonSerializerSettingsSymbol)
                        || !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.NewtonsoftJsonJsonSerializer, out INamedTypeSymbol? jsonSerializerSymbol)
                        || !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.NewtonsoftJsonJsonConvert, out INamedTypeSymbol? jsonConvertSymbol))
                    {
                        return;
                    }

                    PooledHashSet<(IOperation Operation, ISymbol ContainingSymbol)> rootOperationsNeedingAnalysis = PooledHashSet<(IOperation, ISymbol)>.GetInstance();

                    compilationStartAnalysisContext.RegisterOperationBlockStartAction(
                        (OperationBlockStartAnalysisContext operationBlockStartAnalysisContext) =>
                        {
                            var owningSymbol = operationBlockStartAnalysisContext.OwningSymbol;

                            // TODO: Handle case when exactly one of the below rules is configured to skip analysis.
                            if (operationBlockStartAnalysisContext.Options.IsConfiguredToSkipAnalysis(DefinitelyInsecureSettings,
                                    owningSymbol, operationBlockStartAnalysisContext.Compilation) &&
                                operationBlockStartAnalysisContext.Options.IsConfiguredToSkipAnalysis(MaybeInsecureSettings,
                                    owningSymbol, operationBlockStartAnalysisContext.Compilation))
                            {
                                return;
                            }

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    IInvocationOperation invocationOperation =
                                        (IInvocationOperation)operationAnalysisContext.Operation;
                                    if ((jsonSerializerSymbol.Equals(invocationOperation.TargetMethod.ContainingType)
                                            && SecurityHelpers.JsonSerializerInstantiateWithSettingsMethods.Contains(invocationOperation.TargetMethod.Name)
                                            && invocationOperation.TargetMethod.Parameters.Any(
                                                p => jsonSerializerSettingsSymbol.Equals(p.Type)))
                                        || (jsonConvertSymbol.Equals(invocationOperation.TargetMethod.ContainingType)
                                                && SecurityHelpers.JsonSerializerInstantiateWithSettingsMethods.Contains(invocationOperation.TargetMethod.Name)
                                                && invocationOperation.TargetMethod.Parameters.Any(
                                                    p => jsonSerializerSettingsSymbol.Equals(p.Type)))
                                        || jsonSerializerSettingsSymbol.Equals(invocationOperation.TargetMethod.ReturnType))
                                    {
                                        lock (rootOperationsNeedingAnalysis)
                                        {
                                            rootOperationsNeedingAnalysis.Add(
                                                (invocationOperation.GetRoot(), operationAnalysisContext.ContainingSymbol));
                                        }
                                    }
                                },
                                OperationKind.Invocation);

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    IObjectCreationOperation objectCreationOperation =
                                        (IObjectCreationOperation)operationAnalysisContext.Operation;
                                    if (jsonSerializerSettingsSymbol.Equals(objectCreationOperation.Type))
                                    {
                                        lock (rootOperationsNeedingAnalysis)
                                        {
                                            rootOperationsNeedingAnalysis.Add(
                                                (objectCreationOperation.GetRoot(), operationAnalysisContext.ContainingSymbol));
                                        }
                                    }
                                },
                                OperationKind.ObjectCreation);

                            operationBlockStartAnalysisContext.RegisterOperationAction(
                                (OperationAnalysisContext operationAnalysisContext) =>
                                {
                                    IReturnOperation returnOperation = (IReturnOperation)operationAnalysisContext.Operation;
                                    if (jsonSerializerSettingsSymbol.Equals(returnOperation.Type))
                                    {
                                        rootOperationsNeedingAnalysis.Add(
                                            (returnOperation.GetRoot(), operationAnalysisContext.ContainingSymbol));
                                    }
                                },
                                OperationKind.Return);
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
                                        WellKnownTypeNames.NewtonsoftJsonJsonSerializerSettings,
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
                                    DiagnosticDescriptor descriptor;
                                    switch (kvp.Value)
                                    {
                                        case HazardousUsageEvaluationResult.Flagged:
                                            descriptor = DefinitelyInsecureSettings;
                                            break;

                                        case HazardousUsageEvaluationResult.MaybeFlagged:
                                            descriptor = MaybeInsecureSettings;
                                            break;

                                        default:
                                            Debug.Fail($"Unhandled result value {kvp.Value}");
                                            continue;
                                    }

                                    compilationAnalysisContext.ReportDiagnostic(
                                        Diagnostic.Create(
                                            descriptor,
                                            kvp.Key.Location));
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
