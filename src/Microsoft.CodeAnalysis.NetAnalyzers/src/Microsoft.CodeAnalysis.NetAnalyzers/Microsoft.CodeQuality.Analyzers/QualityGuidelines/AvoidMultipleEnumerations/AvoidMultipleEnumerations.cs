// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis;
using static Microsoft.CodeQuality.Analyzers.MicrosoftCodeQualityAnalyzersResources;
using static Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.AvoidMultipleEnumerationsHelpers;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    /// <summary>
    /// CA1851: <inheritdoc cref="AvoidMultipleEnumerationsTitle"/>
    /// </summary>
    internal abstract partial class AvoidMultipleEnumerations : DiagnosticAnalyzer
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
            (WellKnownTypeNames.SystemCollectionsImmutableImmutableArray, nameof(ImmutableArray.ToImmutableArray)),
            (WellKnownTypeNames.SystemCollectionsImmutableImmutableDictionary, nameof(ImmutableDictionary.ToImmutableDictionary)),
            (WellKnownTypeNames.SystemCollectionsImmutableImmutableHashSet, nameof(ImmutableHashSet.ToImmutableHashSet)),
            (WellKnownTypeNames.SystemCollectionsImmutableImmutableList, nameof(ImmutableList.ToImmutableList)),
            (WellKnownTypeNames.SystemCollectionsImmutableImmutableSortedDictionary, nameof(ImmutableSortedDictionary.ToImmutableSortedDictionary)),
            (WellKnownTypeNames.SystemCollectionsImmutableImmutableSortedSet, nameof(ImmutableSortedSet.ToImmutableSortedSet)));

        /// <summary>
        /// All the types under System.Collections.Generic which constructor takes deferred type parameter.
        /// </summary>
        private static readonly ImmutableArray<string> s_constructorsEnumeratedParameterTypes = ImmutableArray.Create(
            WellKnownTypeNames.SystemCollectionsGenericDictionary2,
            WellKnownTypeNames.SystemCollectionsGenericHashSet1,
            WellKnownTypeNames.SystemCollectionsGenericLinkedList1,
            WellKnownTypeNames.SystemCollectionsGenericList1,
            WellKnownTypeNames.SystemCollectionsGenericPriorityQueue2,
            WellKnownTypeNames.SystemCollectionsGenericQueue1,
            WellKnownTypeNames.SystemCollectionsGenericSortedSet1,
            WellKnownTypeNames.SystemCollectionsGenericStack1);

        /// <summary>
        /// Linq methods causing its parameters to be enumerated.
        /// </summary>
        private static readonly ImmutableArray<string> s_enumeratedParametersLinqMethods = ImmutableArray.Create(
            nameof(Enumerable.Aggregate),
            nameof(Enumerable.All),
            nameof(Enumerable.Any),
            nameof(Enumerable.Average),
            nameof(Enumerable.Contains),
            nameof(Enumerable.Count),
            nameof(Enumerable.ElementAt),
            nameof(Enumerable.ElementAtOrDefault),
            nameof(Enumerable.First),
            nameof(Enumerable.FirstOrDefault),
            nameof(Enumerable.Last),
            nameof(Enumerable.LastOrDefault),
            nameof(Enumerable.LongCount),
            nameof(Enumerable.Max),
            nameof(Enumerable.Min),
            nameof(Enumerable.Single),
            nameof(Enumerable.SingleOrDefault),
            nameof(Enumerable.Sum),
            nameof(Enumerable.ToArray),
            nameof(Enumerable.ToDictionary),
            nameof(Enumerable.ToList),
            nameof(Enumerable.ToLookup),
            nameof(Enumerable.SequenceEqual),
            // Only available on .net6 or later
            "MaxBy",
            "MinBy",
            // Only available on .netstandard 2.1 or later
            "ToHashSet");

        /// <summary>
        /// Linq chain methods deferring its parameters to be enumerated, and return a deferred type.
        /// </summary>
        private static readonly ImmutableArray<string> s_linqChainMethods = ImmutableArray.Create(
            nameof(Enumerable.Append),
            nameof(Enumerable.AsEnumerable),
            nameof(Enumerable.Cast),
            nameof(Enumerable.Distinct),
            nameof(Enumerable.GroupBy),
            nameof(Enumerable.OfType),
            nameof(Enumerable.OrderBy),
            nameof(Enumerable.OrderByDescending),
            nameof(Enumerable.Prepend),
            nameof(Enumerable.Reverse),
            nameof(Enumerable.Select),
            nameof(Enumerable.SelectMany),
            nameof(Enumerable.Skip),
            nameof(Enumerable.SkipWhile),
            nameof(Enumerable.Take),
            nameof(Enumerable.TakeWhile),
            nameof(Enumerable.ThenBy),
            nameof(Enumerable.ThenByDescending),
            nameof(Enumerable.Where),
            nameof(Enumerable.Concat),
            nameof(Enumerable.Except),
            nameof(Enumerable.GroupJoin),
            nameof(Enumerable.Intersect),
            nameof(Enumerable.Join),
            nameof(Enumerable.Union),
            nameof(Enumerable.Zip),
            nameof(Enumerable.DefaultIfEmpty),
            // Only available on .net6 or later
            "Chunk",
            "DistinctBy",
            "ExceptBy",
            "IntersectBy",
            "UnionBy",
            // Only available on .netstandard 2.1 or later
            "TakeLast",
            "SkipLast");

        /// <summary>
        /// Special Linq methods that no effect on its parameter, and not return new IEnumerable instance.
        /// </summary>
        private static readonly ImmutableArray<string> s_noEffectLinqChainMethods = ImmutableArray.Create(
            nameof(Enumerable.AsEnumerable));

        /// <summary>
        /// Linq methods don't enumerate deferred type, and is not a linq chain.
        /// </summary>
        private static readonly ImmutableArray<string> s_noEnumerationLinqMethods = ImmutableArray.Create(
            // Only available on .net6 or later
            "TryGetNonEnumeratedCount");

        protected abstract bool IsExpressionOfForEachStatement(SyntaxNode syntax);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(CompilationStartAction);
        }

        private void CompilationStartAction(CompilationStartAnalysisContext context)
        {
            var compilation = context.Compilation;
            var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilation);
            var linqChainMethods = GetLinqMethods(wellKnownTypeProvider, s_linqChainMethods);
            var noEnumerationMethods = GetLinqMethods(wellKnownTypeProvider, s_noEnumerationLinqMethods);
            var enumeratedMethods = GetEnumeratedMethods(wellKnownTypeProvider, s_immutableCollectionsTypeNamesAndConvensionMethods, s_enumeratedParametersLinqMethods, s_constructorsEnumeratedParameterTypes);
            var noEffectLinqChainMethods = GetLinqMethods(wellKnownTypeProvider, s_noEffectLinqChainMethods);
            var additionalDeferredTypes = GetTypes(compilation, s_additionalDeferredTypes);

            // In CFG blocks there is no foreach loop related Operation, so use the
            // the GetEnumerator method to find the foreach loop
            var getEnumeratorSymbols = GetGetEnumeratorMethods(wellKnownTypeProvider);
            context.RegisterOperationBlockStartAction(context => OnOperationBlockStart(
                linqChainMethods,
                noEnumerationMethods,
                enumeratedMethods,
                noEffectLinqChainMethods,
                additionalDeferredTypes,
                getEnumeratorSymbols,
                context));
        }

        private void OnOperationBlockStart(
            ImmutableArray<IMethodSymbol> linqChainMethods,
            ImmutableArray<IMethodSymbol> noEnumerationMethods,
            ImmutableArray<IMethodSymbol> enumeratedMethods,
            ImmutableArray<IMethodSymbol> noEffectLinqChainMethods,
            ImmutableArray<ITypeSymbol> additionalDeferredTypes,
            ImmutableArray<IMethodSymbol> getEnumeratorSymbols,
            OperationBlockStartAnalysisContext context)
        {
            var operationBlocks = context.OperationBlocks;
            if (operationBlocks.IsEmpty)
            {
                return;
            }

            var syntaxTree = operationBlocks[0].Syntax.SyntaxTree;
            var options = context.Options;
            var compilation = context.Compilation;
            var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilation);
            var customizedEnumerationMethods = options.GetEnumerationMethodsOption(
                    MultipleEnumerableDescriptor,
                    syntaxTree,
                    compilation);

            var customizedLinqChainMethods = options.GetLinqChainMethodsOption(
                    MultipleEnumerableDescriptor,
                    syntaxTree,
                    compilation);

            var assumeMethodEnumeratesParameters = options.GetBoolOptionValue(
                EditorConfigOptionNames.AssumeMethodEnumeratesParameters,
                MultipleEnumerableDescriptor,
                syntaxTree,
                compilation,
                defaultValue: false);

            var wellKnownSymbolsInfo = new WellKnownSymbolsInfo(
                linqChainMethods,
                noEnumerationMethods,
                enumeratedMethods,
                noEffectLinqChainMethods,
                additionalDeferredTypes,
                getEnumeratorSymbols,
                customizedEnumerationMethods,
                customizedLinqChainMethods,
                assumeMethodEnumeratesParameters);

            var potentialDiagnosticOperationsBuilder = PooledHashSet<IOperation>.GetInstance();
            context.RegisterOperationAction(
                context => CollectPotentialDiagnosticOperations(
                    context,
                    wellKnownSymbolsInfo,
                    potentialDiagnosticOperationsBuilder),
                OperationKind.ParameterReference,
                OperationKind.LocalReference);

            context.RegisterOperationBlockEndAction(
                context => Analyze(
                    context,
                    wellKnownTypeProvider,
                    wellKnownSymbolsInfo,
                    potentialDiagnosticOperationsBuilder));
        }

        private static void CollectPotentialDiagnosticOperations(
            OperationAnalysisContext context,
            WellKnownSymbolsInfo wellKnownSymbolsInfo,
            PooledHashSet<IOperation> builder)
        {
            var operation = context.Operation;
            if (IsDeferredType(operation.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes)
                && IsEnumerated(operation, wellKnownSymbolsInfo))
            {
                builder.Add(operation);
            }
        }

        private static bool IsEnumerated(IOperation operation, WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            var (linqChainTailOperation, enumerationCount) = SkipLinqChainAndConversionMethod(operation, wellKnownSymbolsInfo);
            if (enumerationCount == EnumerationCount.None)
            {
                return false;
            }

            if (enumerationCount > EnumerationCount.Zero)
            {
                return true;
            }

            return IsOperationEnumeratedByInvocation(linqChainTailOperation, wellKnownSymbolsInfo)
                || IsOperationEnumeratedByForEachLoop(linqChainTailOperation, wellKnownSymbolsInfo);
        }

        private void Analyze(
            OperationBlockAnalysisContext context,
            WellKnownTypeProvider wellKnownTypeProvider,
            WellKnownSymbolsInfo wellKnownSymbolsInfo,
            PooledHashSet<IOperation> potentialDiagnosticOperations)
        {
            try
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

                var analysisResult = GlobalFlowStateDictionaryAnalysis.TryGetOrComputeResult(
                    cfg,
                    context.OwningSymbol,
                    analysisContext => new AvoidMultipleEnumerationsFlowStateDictionaryFlowOperationVisitor(
                        this,
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

                    foreach (var (_, trackedInvocationSet) in result.TrackedEntities)
                    {
                        // Report if
                        // 1. EnumerationCount is two or more times.
                        // 2. There are two or more operations that might be involved.
                        // (Note: 2 is an aggressive way to report diagnostic, because it is not guaranteed that happens on all the code path)
                        if (trackedInvocationSet.EnumerationCount == EnumerationCount.TwoOrMoreTime || trackedInvocationSet.Operations.Count > 1)
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
            finally
            {
                potentialDiagnosticOperations.Free(CancellationToken.None);
            }
        }
    }
}
