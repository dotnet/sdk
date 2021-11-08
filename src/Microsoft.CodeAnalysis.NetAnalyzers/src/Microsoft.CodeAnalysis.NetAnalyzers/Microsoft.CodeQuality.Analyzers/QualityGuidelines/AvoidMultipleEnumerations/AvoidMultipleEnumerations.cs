// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis;
using static Microsoft.CodeQuality.Analyzers.MicrosoftCodeQualityAnalyzersResources;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public abstract partial class AvoidMultipleEnumerations : DiagnosticAnalyzer
    {
        private const string RuleId = "CA1851";

        private static readonly DiagnosticDescriptor MultipleEnumerableDescriptor = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AvoidMultipleEnumerationsTitle)),
            CreateLocalizableResourceString(nameof(AvoidMultipleEnumerationsMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.Disabled,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(MultipleEnumerableDescriptor);

        /// <summary>
        /// Additional types that has the ability to defer enumeration.
        /// </summary>
        private static readonly ImmutableArray<string> s_additionalDeferredTypes =
            ImmutableArray.Create(WellKnownTypeNames.SystemLinqIOrderedEnumerable1);

        /// <summary>
        /// All the immutable collections that have a conversion method from IEnumerable.
        /// </summary>
        private static readonly ImmutableArray<(string typeName, string methodName)> s_immutableCollectionsTypeNamesAndConvensionMethods = ImmutableArray.Create(
            ("System.Collections.Immutable.ImmutableArray", "ToImmutableArray"),
            ("System.Collections.Immutable.ImmutableDictionary", "ToImmutableDictionary"),
            ("System.Collections.Immutable.ImmutableHashSet", "ToImmutableHashSet"),
            ("System.Collections.Immutable.ImmutableList", "ToImmutableList"),
            ("System.Collections.Immutable.ImmutableSortedDictionary", "ToImmutableSortedDictionary"),
            ("System.Collections.Immutable.ImmutableSortedSet", "ToImmutableSortedSet"));

        /// <summary>
        /// Linq methods causing its first parameter to be enumerated.
        /// </summary>
        private static readonly ImmutableArray<string> s_linqOneParameterEnumeratedMethods = ImmutableArray.Create(
            "Aggregate",
            "All",
            "Any",
            "Average",
            "Contains",
            "Count",
            "DefaultIfEmpty",
            "ElementAt",
            "ElementAtOrDefault",
            "First",
            "FirstOrDefault",
            "Last",
            "LastOrDefault",
            "LongCount",
            "Max",
            "Min",
            "Single",
            "SingleOrDefault",
            "Sum",
            "ToArray",
            "ToDictionary",
            "ToHashSet",
            "ToList",
            "ToLookup");

        /// <summary>
        /// Linq methods causing its first two parameters to be enumerated.
        /// </summary>
        private static readonly ImmutableArray<string> s_linqTwoParametersEnumeratedMethods = ImmutableArray.Create(
            "SequenceEqual");

        /// <summary>
        /// Linq methods deferring its first parameter to be enumerated.
        /// </summary>
        private static readonly ImmutableArray<string> s_linqOneParameterDeferredMethods = ImmutableArray.Create(
            "Append",
            "Cast",
            "Distinct",
            "GroupBy",
            "OfType",
            "OrderBy",
            "OrderByDescending",
            "Prepend",
            "Reverse",
            "Select",
            "SelectMany",
            "Skip",
            "SkipLast",
            "SkipWhile",
            "Take",
            "TakeLast",
            "TakeWhile",
            "ThenBy",
            "ThenByDescending",
            "Where");

        /// <summary>
        /// Linq methods deferring its first two parameters to be enumerated.
        /// </summary>
        private static readonly ImmutableArray<string> s_linqTwoParametersDeferredMethods = ImmutableArray.Create(
            "Concat",
            "Except",
            "GroupJoin",
            "Intersect",
            "Join",
            "Union");

        internal abstract GlobalFlowStateDictionaryFlowOperationVisitor CreateOperationVisitor(
            GlobalFlowStateDictionaryAnalysisContext context,
            WellKnownSymbolsInfo wellKnownSymbolsInfo);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(CompilationStartAction);
        }

        private void CompilationStartAction(CompilationStartAnalysisContext context)
        {
            context.RegisterOperationBlockStartAction(OnOperationBlockStart);
        }

        private void OnOperationBlockStart(OperationBlockStartAnalysisContext operationBlockStartAnalysisContext)
        {
            var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(operationBlockStartAnalysisContext.Compilation);

            var oneParameterDeferredMethods = GetOneParameterDeferredMethods(wellKnownTypeProvider);
            var twoParametersDeferredMethods = GetTwoParametersDeferredMethods(wellKnownTypeProvider);

            var oneParameterEnumeratedMethods = GetOneParameterEnumeratedMethods(wellKnownTypeProvider);
            var twoParametersEnumeratedMethods = GetTwoParametersEnumeratedMethods(wellKnownTypeProvider);

            var compilation = operationBlockStartAnalysisContext.Compilation;
            var additionalDeferTypes = GetTypes(compilation, s_additionalDeferredTypes);

            // In CFG blocks there is no foreach loop related Operation, so use the
            // the GetEnumerator method to find the foreach loop
            var getEnumeratorSymbols = GetGetEnumeratorMethods(wellKnownTypeProvider);

            var wellKnownSymbolsInfo = new WellKnownSymbolsInfo(
                oneParameterDeferredMethods,
                twoParametersDeferredMethods,
                oneParameterEnumeratedMethods,
                twoParametersEnumeratedMethods,
                additionalDeferTypes,
                getEnumeratorSymbols);

            var potentialDiagnosticOperationsBuilder = ImmutableHashSet.CreateBuilder<IOperation>();
            operationBlockStartAnalysisContext.RegisterOperationAction(
                context => CollectPotentialDiagnosticOperations(
                    context,
                    wellKnownSymbolsInfo,
                    potentialDiagnosticOperationsBuilder),
                OperationKind.ParameterReference,
                OperationKind.LocalReference);

            operationBlockStartAnalysisContext.RegisterOperationBlockEndAction(
                context => Analyze(
                    context,
                    wellKnownTypeProvider,
                    wellKnownSymbolsInfo,
                    potentialDiagnosticOperationsBuilder));
        }

        private static void CollectPotentialDiagnosticOperations(
            OperationAnalysisContext context,
            WellKnownSymbolsInfo wellKnownSymbolsInfo,
            ImmutableHashSet<IOperation>.Builder builder)
        {
            var operation = context.Operation;
            if (IsDeferredType(operation.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                var isEnumerated = IsOperationEnumeratedByMethodInvocation(operation, wellKnownSymbolsInfo) || IsOperationEnumeratedByForEachLoop(operation, wellKnownSymbolsInfo);
                if (isEnumerated)
                    builder.Add(operation);
            }
        }

        private void Analyze(
            OperationBlockAnalysisContext context,
            WellKnownTypeProvider wellKnownTypeProvider,
            WellKnownSymbolsInfo wellKnownSymbolsInfo,
            ImmutableHashSet<IOperation>.Builder potentialDiagnosticOperationsBuilder)
        {
            var potentialDiagnosticOperations = potentialDiagnosticOperationsBuilder.ToImmutable();
            if (potentialDiagnosticOperations.IsEmpty)
            {
                return;
            }

            var cfg = context.OperationBlocks.GetControlFlowGraph();
            if (cfg == null)
            {
                return;
            }

            var analysisResult = GlobalFlowStateDictionaryAnalysis.TryGetOrComputeResult(
                cfg,
                context.OwningSymbol,
                analysisContext => CreateOperationVisitor(
                    analysisContext,
                    wellKnownSymbolsInfo),
                wellKnownTypeProvider,
                context.Options,
                MultipleEnumerableDescriptor,
                // We are only interested in the state of parameters & locals. So no need to pessimistic for instance field.
                pessimisticAnalysis: false);

            if (analysisResult == null)
            {
                return;
            }

            using var diagnosticOperations = PooledHashSet<IOperation>.GetInstance();
            foreach (var operation in potentialDiagnosticOperations)
            {
                var result = analysisResult[operation.Kind, operation.Syntax];
                if (result.Kind != GlobalFlowStateDictionaryAnalysisValueKind.Known)
                {
                    continue;
                }

                foreach (var kvp in result.TrackedEntities)
                {
                    var trackedInvocationSet = kvp.Value;
                    if (trackedInvocationSet.EnumerationCount == InvocationCount.TwoOrMoreTime)
                    {
                        foreach (var trackedOperation in trackedInvocationSet.Operations)
                        {
                            diagnosticOperations.Add(trackedOperation);
                        }
                    }
                }
            }

            foreach (var operation in diagnosticOperations)
            {
                context.ReportDiagnostic(operation.CreateDiagnostic(MultipleEnumerableDescriptor));
            }
        }
    }
}
