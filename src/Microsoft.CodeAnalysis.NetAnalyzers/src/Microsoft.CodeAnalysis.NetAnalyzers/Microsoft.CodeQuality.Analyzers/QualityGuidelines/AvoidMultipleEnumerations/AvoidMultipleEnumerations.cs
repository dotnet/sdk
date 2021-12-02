// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Buffers;
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
using static Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.AvoidMultipleEnumerationsHelper;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
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
            ("System.Collections.Immutable.ImmutableArray", nameof(ImmutableArray.ToImmutableArray)),
            ("System.Collections.Immutable.ImmutableDictionary", nameof(ImmutableDictionary.ToImmutableDictionary)),
            ("System.Collections.Immutable.ImmutableHashSet", nameof(ImmutableHashSet.ToImmutableHashSet)),
            ("System.Collections.Immutable.ImmutableList", nameof(ImmutableList.ToImmutableList)),
            ("System.Collections.Immutable.ImmutableSortedDictionary", nameof(ImmutableSortedDictionary.ToImmutableSortedDictionary)),
            ("System.Collections.Immutable.ImmutableSortedSet", nameof(ImmutableSortedSet.ToImmutableSortedSet)));

        /// <summary>
        /// Linq methods causing its first parameter to be enumerated.
        /// </summary>
        private static readonly ImmutableArray<string> s_linqOneParameterEnumeratedMethods = ImmutableArray.Create(
            nameof(Enumerable.Aggregate),
            nameof(Enumerable.All),
            nameof(Enumerable.Any),
            nameof(Enumerable.Average),
            nameof(Enumerable.Contains),
            nameof(Enumerable.Count),
            nameof(Enumerable.DefaultIfEmpty),
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
            // Only available on .net6 or later
            "MaxBy",
            "MinBy",
            // Only available on .netstandard 2.1 or later
            "ToHashSet");

        /// <summary>
        /// Linq methods causing its first two parameters to be enumerated.
        /// </summary>
        private static readonly ImmutableArray<string> s_linqTwoParametersEnumeratedMethods = ImmutableArray.Create(
            nameof(Enumerable.SequenceEqual));

        /// <summary>
        /// Linq methods deferring its first parameter to be enumerated.
        /// </summary>
        private static readonly ImmutableArray<string> s_linqOneParameterDeferredMethods = ImmutableArray.Create(
            nameof(Enumerable.Append),
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
            // Only available on .net6 or later
            "Chunk",
            "DistinctBy",
            "TryGetNonEnumeratedCount",
            // Only available on .netstandard 2.1 or later
            "TakeLast",
            "SkipLast");

        /// <summary>
        /// Linq methods deferring its first two parameters to be enumerated.
        /// </summary>
        private static readonly ImmutableArray<string> s_linqTwoParametersDeferredMethods = ImmutableArray.Create(
            nameof(Enumerable.Concat),
            nameof(Enumerable.Except),
            nameof(Enumerable.GroupJoin),
            nameof(Enumerable.Intersect),
            nameof(Enumerable.Join),
            nameof(Enumerable.Union),
            nameof(Enumerable.Zip),
            // Only available on .net6 or later
            "ExceptBy",
            "IntersectBy",
            "UnionBy");

        private static readonly ImmutableArray<string> s_linqThreeParametersDeferredMethods = ImmutableArray.Create(
            nameof(Enumerable.Zip));

        private static readonly ImmutableArray<string> s_linqNoEffectMethods = ImmutableArray.Create(
            nameof(Enumerable.AsEnumerable));

        protected abstract GlobalFlowStateDictionaryFlowOperationVisitor CreateOperationVisitor(
            GlobalFlowStateDictionaryAnalysisContext context,
            WellKnownSymbolsInfo wellKnownSymbolsInfo);

        protected abstract AvoidMultipleEnumerationsHelper AvoidMultipleEnumerationsHelper { get; }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(CompilationStartAction);
        }

        private void CompilationStartAction(CompilationStartAnalysisContext context)
            => context.RegisterOperationBlockStartAction(OnOperationBlockStart);

        private void OnOperationBlockStart(OperationBlockStartAnalysisContext operationBlockStartAnalysisContext)
        {
            var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(operationBlockStartAnalysisContext.Compilation);

            var oneParameterDeferredMethods = GetLinqMethods(wellKnownTypeProvider, s_linqOneParameterDeferredMethods);
            var twoParametersDeferredMethods = GetLinqMethods(wellKnownTypeProvider, s_linqTwoParametersDeferredMethods);
            var threeParametersDeferredMethods = GetLinqMethods(wellKnownTypeProvider, s_linqThreeParametersDeferredMethods);

            var oneParameterEnumeratedMethods = GetOneParameterEnumeratedMethods(wellKnownTypeProvider, s_immutableCollectionsTypeNamesAndConvensionMethods, s_linqOneParameterEnumeratedMethods);
            var twoParametersEnumeratedMethods = GetLinqMethods(wellKnownTypeProvider, s_linqTwoParametersEnumeratedMethods);

            var noEffectMethods = GetLinqMethods(wellKnownTypeProvider, s_linqNoEffectMethods);

            var compilation = operationBlockStartAnalysisContext.Compilation;
            var additionalDeferTypes = GetTypes(compilation, s_additionalDeferredTypes);

            // In CFG blocks there is no foreach loop related Operation, so use the
            // the GetEnumerator method to find the foreach loop
            var getEnumeratorSymbols = GetGetEnumeratorMethods(wellKnownTypeProvider);

            var wellKnownSymbolsInfo = new WellKnownSymbolsInfo(
                oneParameterDeferredMethods,
                twoParametersDeferredMethods,
                threeParametersDeferredMethods,
                oneParameterEnumeratedMethods,
                twoParametersEnumeratedMethods,
                noEffectMethods,
                additionalDeferTypes,
                getEnumeratorSymbols);

            var potentialDiagnosticOperationsBuilder = PooledHashSet<IOperation>.GetInstance();
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

        private void CollectPotentialDiagnosticOperations(
            OperationAnalysisContext context,
            WellKnownSymbolsInfo wellKnownSymbolsInfo,
            PooledHashSet<IOperation> builder)
        {
            var operation = context.Operation;
            if (IsDeferredType(operation.Type?.OriginalDefinition, wellKnownSymbolsInfo.AdditionalDeferredTypes))
            {
                var isEnumerated = AvoidMultipleEnumerationsHelper.IsOperationEnumeratedByMethodInvocation(operation, wellKnownSymbolsInfo)
                    || AvoidMultipleEnumerationsHelper.IsOperationEnumeratedByForEachLoop(operation, wellKnownSymbolsInfo);
                if (isEnumerated)
                    builder.Add(operation);
            }
        }

        private void Analyze(
            OperationBlockAnalysisContext context,
            WellKnownTypeProvider wellKnownTypeProvider,
            WellKnownSymbolsInfo wellKnownSymbolsInfo,
            PooledHashSet<IOperation> potentialDiagnosticOperations)
        {
            if (potentialDiagnosticOperations.Count == 0)
            {
                potentialDiagnosticOperations.Free(CancellationToken.None);
                return;
            }

            var cfg = context.OperationBlocks.GetControlFlowGraph();
            if (cfg == null)
            {
                potentialDiagnosticOperations.Free(CancellationToken.None);
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
                potentialDiagnosticOperations.Free(CancellationToken.None);
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
                    if (trackedInvocationSet.EnumerationCount == InvocationCount.TwoOrMoreTime || trackedInvocationSet.Operations.Count > 1)
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

            potentialDiagnosticOperations.Free(CancellationToken.None);
        }

        private static ImmutableArray<IMethodSymbol> GetOneParameterEnumeratedMethods(WellKnownTypeProvider wellKnownTypeProvider,
            ImmutableArray<(string typeName, string methodName)> typeAndMethodNames,
            ImmutableArray<string> linqMethodNames)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            GetImmutableCollectionConversionMethods(wellKnownTypeProvider, typeAndMethodNames, builder);
            GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, linqMethodNames, builder);
            return builder.ToImmutable();
        }

        private static void GetImmutableCollectionConversionMethods(
            WellKnownTypeProvider wellKnownTypeProvider,
            ImmutableArray<(string, string)> typeAndMethodNames,
            ArrayBuilder<IMethodSymbol> builder)
        {
            // Get immutable collection conversion method, like ToImmutableArray()
            foreach (var (typeName, methodName) in typeAndMethodNames)
            {
                if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(typeName, out var type))
                {
                    var methods = type.GetMembers(methodName);
                    foreach (var method in methods)
                    {
                        // Usually there are two overloads for these methods, like ToImmutableArray,
                        // it has two overloads, one convert from ImmutableArray.Builder and one covert from IEnumerable<T>
                        // and we only want the last one
                        if (method is IMethodSymbol { Parameters: { Length: > 0 } parameters, IsExtensionMethod: true } methodSymbol
                            && parameters[0].Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                        {
                            builder.AddRange(methodSymbol);
                        }
                    }
                }
            }
        }

        private static ImmutableArray<IMethodSymbol> GetGetEnumeratorMethods(WellKnownTypeProvider wellKnownTypeProvider)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();

            if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsIEnumerable, out var nonGenericIEnumerable))
            {
                var method = nonGenericIEnumerable.GetMembers(WellKnownMemberNames.GetEnumeratorMethodName).FirstOrDefault();
                if (method is IMethodSymbol methodSymbol)
                {
                    builder.Add(methodSymbol);
                }
            }

            if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIEnumerable1, out var genericIEnumerable))
            {
                var method = genericIEnumerable.GetMembers(WellKnownMemberNames.GetEnumeratorMethodName).FirstOrDefault();
                if (method is IMethodSymbol methodSymbol)
                {
                    builder.Add(methodSymbol);
                }
            }

            return builder.ToImmutable();
        }

        private static ImmutableArray<ITypeSymbol> GetTypes(Compilation compilation, ImmutableArray<string> typeNames)
        {
            using var builder = ArrayBuilder<ITypeSymbol>.GetInstance();
            foreach (var name in typeNames)
            {
                if (compilation.TryGetOrCreateTypeByMetadataName(name, out var typeSymbol))
                {
                    builder.Add(typeSymbol);
                }
            }

            return builder.ToImmutable();
        }

        private static ImmutableArray<IMethodSymbol> GetLinqMethods(WellKnownTypeProvider wellKnownTypeProvider, ImmutableArray<string> methodNames)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, methodNames, builder);
            return builder.ToImmutable();
        }

        private static void GetWellKnownMethods(
            WellKnownTypeProvider wellKnownTypeProvider,
            string typeName,
            ImmutableArray<string> methodNames,
            ArrayBuilder<IMethodSymbol> builder)
        {
            if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(typeName, out var type))
            {
                foreach (var methodName in methodNames)
                {
                    builder.AddRange(type.GetMembers(methodName).OfType<IMethodSymbol>());
                }
            }
        }
    }
}
