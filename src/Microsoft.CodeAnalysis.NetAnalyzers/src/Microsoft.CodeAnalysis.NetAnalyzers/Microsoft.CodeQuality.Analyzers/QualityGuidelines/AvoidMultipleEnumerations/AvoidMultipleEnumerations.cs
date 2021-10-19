// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.FlowAnalysis.Analysis.InvocationCountAnalysis;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public abstract partial class AvoidMultipleEnumerations : DiagnosticAnalyzer
    {
        private const string RuleId = "CA1850";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(
            nameof(MicrosoftCodeQualityAnalyzersResources.AvoidMultipleEnumerationsTitle),
            MicrosoftCodeQualityAnalyzersResources.ResourceManager,
            typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(
            nameof(MicrosoftCodeQualityAnalyzersResources.AvoidMultipleEnumerationsMessage),
            MicrosoftCodeQualityAnalyzersResources.ResourceManager,
            typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly DiagnosticDescriptor MultipleEnumerableDescriptor = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Performance,
            RuleLevel.Disabled,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(MultipleEnumerableDescriptor);

        private static readonly ImmutableArray<string> s_wellKnownLinqMethodsCauseEnumeration = ImmutableArray.Create(
            "System.Linq.Enumerable.Aggregate",
            "System.Linq.Enumerable.All",
            "System.Linq.Enumerable.Any",
            "System.Linq.Enumerable.Average",
            "System.Linq.Enumerable.Contains",
            "System.Linq.Enumerable.Count",
            "System.Linq.Enumerable.DefaultIfEmpty",
            "System.Linq.Enumerable.ElementAt",
            "System.Linq.Enumerable.ElementAtOrDefault",
            "System.Linq.Enumerable.First",
            "System.Linq.Enumerable.FirstOrDefault",
            "System.Linq.Enumerable.Last",
            "System.Linq.Enumerable.LastOrDefault",
            "System.Linq.Enumerable.LongCount",
            "System.Linq.Enumerable.Max",
            "System.Linq.Enumerable.Min",
            "System.Linq.Enumerable.SequenceEqual",
            "System.Linq.Enumerable.Single",
            "System.Linq.Enumerable.SingleOrDefault",
            "System.Linq.Enumerable.Sum",
            "System.Linq.Enumerable.ToArray",
            "System.Linq.Enumerable.ToDictionary",
            "System.Linq.Enumerable.ToHashSet",
            "System.Linq.Enumerable.ToList",
            "System.Linq.Enumerable.ToLookup");

        private static readonly ImmutableArray<string> s_wellKnownDelayExecutionLinqMethod = ImmutableArray.Create(
            "System.Linq.Enumerable.Append",
            "System.Linq.Enumerable.AsEnumerable",
            "System.Linq.Enumerable.Cast",
            "System.Linq.Enumerable.Concat",
            "System.Linq.Enumerable.Distinct",
            "System.Linq.Enumerable.Except",
            "System.Linq.Enumerable.GroupBy",
            "System.Linq.Enumerable.GroupJoin",
            "System.Linq.Enumerable.Intersect",
            "System.Linq.Enumerable.Join",
            "System.Linq.Enumerable.OfType",
            "System.Linq.Enumerable.OrderBy",
            "System.Linq.Enumerable.OrderByDescending",
            "System.Linq.Enumerable.Prepend",
            "System.Linq.Enumerable.Range",
            "System.Linq.Enumerable.Repeat",
            "System.Linq.Enumerable.Reverse",
            "System.Linq.Enumerable.Select",
            "System.Linq.Enumerable.SelectMany",
            "System.Linq.Enumerable.Skip",
            "System.Linq.Enumerable.SkipLast",
            "System.Linq.Enumerable.SkipWhile",
            "System.Linq.Enumerable.Take",
            "System.Linq.Enumerable.TakeLast",
            "System.Linq.Enumerable.ThenBy",
            "System.Linq.Enumerable.ThenByDescending",
            "System.Linq.Enumerable.Union",
            "System.Linq.Enumerable.Where");

        internal abstract InvocationCountDataFlowOperationVisitor CreateOperationVisitor(
            InvocationCountAnalysisContext context,
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

            using var potentialDiagnosticOperationsBuilder = PooledHashSet<IOperation>.GetInstance();
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
            PooledHashSet<IOperation> builder)
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
            PooledHashSet<IOperation> potentialDiagnosticOperations)
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

            var getEnumeratorSymbol = wellKnownTypeProvider
                .GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIEnumerable1)
                ?.GetMembers(WellKnownMemberNames.GetEnumeratorMethodName).FirstOrDefault() as IMethodSymbol;

            var analysisResult = InvocationCountAnalysis.TryGetOrComputeResult(
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

            using var diagnoticOperations = PooledHashSet<IOperation>.GetInstance();

            foreach (var block in cfg.Blocks)
            {
                var result = analysisResult[block].Data;
                if (result.IsEmpty)
                {
                    continue;
                }

                // AnalysisResult is shared per block, so just pick the first one to report diagnostic
                var globalAnalysisResult = result.First().Value;
                if (globalAnalysisResult.Kind != InvocationCountAnalysisValueKind.Known)
                {
                    continue;
                }

                foreach (var kvp in globalAnalysisResult.TrackedEntities)
                {
                    var trackedInvocationSet = kvp.Value;
                    if (trackedInvocationSet.TotalCount == InvocationCount.TwoOrMoreTime)
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
