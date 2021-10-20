// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.FlowAnalysis.Analysis.GlobalFlowStateDictionaryAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public abstract partial class AvoidMultipleEnumerations : DiagnosticAnalyzer
    {
        private const string RuleId = "CA1850";

        private static readonly DiagnosticDescriptor MultipleEnumerableDescriptor = DiagnosticDescriptorHelper.Create(
            RuleId,
            MicrosoftCodeQualityAnalyzersResources.CreateLocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidMultipleEnumerationsTitle)),
            MicrosoftCodeQualityAnalyzersResources.CreateLocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidMultipleEnumerationsMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.Disabled,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(MultipleEnumerableDescriptor);

        // The parameter name used in the enumeration method to indicate the source.
        // e.g.
        // public static TSource First<TSource>(this System.Collections.Generic.IEnumerable<TSource> source, Func<TSource, bool> predicate);
        private const string parameterNameInLinqMethod = "source";

        /// <summary>
        /// All the collections that have a convertion method from IEnumerable.
        /// e.g. ToImmutableArray()
        /// </summary>
        private static readonly ImmutableArray<(string typeName, string methodName)> s_wellKnownImmutableCollectionsHaveCovertMethod = ImmutableArray.Create(
            ("System.Collections.Immutable.ImmutableArray", "ToImmutableArray"),
            ("System.Collections.Immutable.ImmutableDictionary", "ToImmutableDictionary"),
            ("System.Collections.Immutable.ImmutableHashSet", "ToImmutableHashSet"),
            ("System.Collections.Immutable.ImmutableList", "ToImmutableList"),
            ("System.Collections.Immutable.ImmutableSortedDictionary", "ToImmutableSortedDictionary"),
            ("System.Collections.Immutable.ImmutableSortedSet", "ToImmutableSortedSet"));

        private static readonly ImmutableArray<string> s_wellKnownLinqMethodsCausingEnumeration = ImmutableArray.Create(
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
            "SequenceEqual",
            "Single",
            "SingleOrDefault",
            "Sum",
            "ToArray",
            "ToDictionary",
            "ToHashSet",
            "ToList",
            "ToLookup");

        private static readonly ImmutableArray<string> s_wellKnownDelayExecutionLinqMethod = ImmutableArray.Create(
            "Append",
            "AsEnumerable",
            "Cast",
            "Concat",
            "Distinct",
            "Except",
            "GroupBy",
            "GroupJoin",
            "Intersect",
            "Join",
            "OfType",
            "OrderBy",
            "OrderByDescending",
            "Prepend",
            "Range",
            "Repeat",
            "Reverse",
            "Select",
            "SelectMany",
            "Skip",
            "SkipLast",
            "SkipWhile",
            "Take",
            "TakeLast",
            "ThenBy",
            "ThenByDescending",
            "Union",
            "Where");

        internal abstract GlobalFlowStateDictionaryFlowOperationVisitor CreateOperationVisitor(
            GlobalFlowStateDictionaryAnalysisContext context,
            ImmutableArray<IMethodSymbol> wellKnownDelayExecutionMethods,
            ImmutableArray<IMethodSymbol> wellKnownEnumerationMethods,
            IMethodSymbol? getEnumeratorMethod);

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
            var wellKnownEnumerationMethods = GetWellKnownEnumerationMethods(wellKnownTypeProvider);
            var wellKnownDelayExecutionMethods = GetWellKnownDelayExecutionMethod(wellKnownTypeProvider);

            var potentialDiagnosticOperationsBuilder = new HashSet<IOperation>();
            operationBlockStartAnalysisContext.RegisterOperationAction(
                context => CollectPotentialDiagnosticOperations(context, wellKnownDelayExecutionMethods, wellKnownEnumerationMethods, potentialDiagnosticOperationsBuilder),
                OperationKind.ParameterReference,
                OperationKind.LocalReference);

            operationBlockStartAnalysisContext.RegisterOperationBlockEndAction(
                context => Analyze(context, wellKnownTypeProvider, wellKnownDelayExecutionMethods, wellKnownEnumerationMethods, potentialDiagnosticOperationsBuilder));
        }

        private static void CollectPotentialDiagnosticOperations(
            OperationAnalysisContext context,
            ImmutableArray<IMethodSymbol> wellKnownDelayExecutionMethods,
            ImmutableArray<IMethodSymbol> wellKnownEnumerationMethods,
            HashSet<IOperation> builder)
        {
            var operation = context.Operation;
            if (operation.Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                && (IsOperationEnumeratedByMethodInvocation(operation, wellKnownDelayExecutionMethods, wellKnownEnumerationMethods)
                     || IsOperationEnumeratedByForEachLoop(operation, wellKnownDelayExecutionMethods)))
            {
                builder.Add(operation);
            }
        }

        private void Analyze(
            OperationBlockAnalysisContext context,
            WellKnownTypeProvider wellKnownTypeProvider,
            ImmutableArray<IMethodSymbol> wellKnownDelayExecutionMethods,
            ImmutableArray<IMethodSymbol> wellKnownEnumerationMethods,
            HashSet<IOperation> potentialDiagnosticOperations)
        {
            if (potentialDiagnosticOperations.Count == 0)
            {
                return;
            }

            var cfg = context.OperationBlocks.GetControlFlowGraph();
            if (cfg == null)
            {
                return;
            }

            // In CFG blocks there is no foreach loop related Operation, so use the
            // the GetEnumerator method to find the foreach loop
            var getEnumeratorSymbol = wellKnownTypeProvider
                .GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIEnumerable1)
                ?.GetMembers(WellKnownMemberNames.GetEnumeratorMethodName).FirstOrDefault() as IMethodSymbol;

            var analysisResult = GlobalFlowStateDictionaryAnalysis.TryGetOrComputeResult(
                cfg,
                context.OwningSymbol,
                analysisContext => CreateOperationVisitor(analysisContext, wellKnownDelayExecutionMethods, wellKnownEnumerationMethods, getEnumeratorSymbol),
                wellKnownTypeProvider,
                context.Options,
                MultipleEnumerableDescriptor,
                pessimisticAnalysis: false);

            if (analysisResult == null)
            {
                return;
            }

            var diagnoticOperations = new HashSet<IOperation>();
            foreach (var block in cfg.Blocks)
            {
                var result = analysisResult[block].Data;
                if (result.IsEmpty)
                {
                    continue;
                }

                // AnalysisResult is shared per block, so just pick the first one to report diagnostic
                var globalAnalysisResult = result.First().Value;
                if (globalAnalysisResult.Kind != GlobalFlowStateDictionaryAnalysisValueKind.Known)
                {
                    continue;
                }

                foreach (var kvp in globalAnalysisResult.TrackedEntities)
                {
                    var trackedInvocationSet = kvp.Value;
                    if (trackedInvocationSet.EnumerationCount == InvocationCount.TwoOrMoreTime)
                    {
                        foreach (var operation in trackedInvocationSet.Operations)
                        {
                            diagnoticOperations.Add(operation);
                        }
                    }
                }
            }

            foreach (var operation in diagnoticOperations)
            {
                context.ReportDiagnostic(operation.CreateDiagnostic(MultipleEnumerableDescriptor));
            }
        }
    }
}
