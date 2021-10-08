// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed partial class AvoidMultipleEnumerations : DiagnosticAnalyzer
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

        internal static readonly DiagnosticDescriptor MultipleEnumerableDescriptor = DiagnosticDescriptorHelper.Create(
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

        private static readonly ImmutableArray<string> s_wellKnownLinqMethods = ImmutableArray.Create(
            "System.Linq.Enumerable.Aggregate",
            "System.Linq.Enumerable.All",
            "System.Linq.Enumerable.Any",
            "System.Linq.Enumerable.Average",
            "System.Linq.Enumerable.Contains",
            "System.Linq.Enumerable.Count",
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

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(CompilationStartAction);
        }

        private static void CompilationStartAction(CompilationStartAnalysisContext context) => context.RegisterOperationBlockAction(OperationBlockAction);

        private static void OperationBlockAction(OperationBlockAnalysisContext context) => Analyze(context);

        private static void Analyze(OperationBlockAnalysisContext context)
        {
            var cfg = context.OperationBlocks.GetControlFlowGraph();
            if (cfg == null)
            {
                return;
            }

            var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
            var wellKnownLinqMethodsCauseEnumeration = GetWellKnownLinqMethodsCausingEnumeration(wellKnownTypeProvider);

            using var blockToTargetInvocationOperationsMapBuilder = PooledDictionary<BasicBlock, ImmutableArray<IOperation>>.GetInstance();
            var basicBlocks = cfg.Blocks;
            foreach (var block in basicBlocks)
            {
                if (!block.IsReachable || block.Operations.IsEmpty)
                {
                    continue;
                }

                using var arrayBuilder = ArrayBuilder<IOperation>.GetInstance();
                foreach (var operation in block.Operations)
                {
                    var immediateExecutedInvocationOperations = GetOperationsCausingEnumeration(operation, wellKnownLinqMethodsCauseEnumeration);
                    if (immediateExecutedInvocationOperations.IsEmpty)
                    {
                        continue;
                    }

                    arrayBuilder.AddRange(immediateExecutedInvocationOperations);
                }

                blockToTargetInvocationOperationsMapBuilder.Add(block, arrayBuilder.ToImmutable());
            }

            if (blockToTargetInvocationOperationsMapBuilder.Count == 0)
            {
                return;
            }

            var blockToTargetInvocationOperationsMap = blockToTargetInvocationOperationsMapBuilder.ToImmutableDictionary();

            var analysisResult = GlobalFlowStateAnalysis.TryGetOrComputeResult(
                cfg,
                context.OwningSymbol,
                globalFlowStateAnalysisContext => CreateOperationVisitor(globalFlowStateAnalysisContext, wellKnownLinqMethodsCauseEnumeration),
                wellKnownTypeProvider,
                context.Options,
                MultipleEnumerableDescriptor,
                performValueContentAnalysis: true,
                pessimisticAnalysis: false,
                out var valueContentAnalysisResult);

            if (analysisResult == null)
            {
                return;
            }

            foreach (var block in basicBlocks)
            {
                var blockResult = analysisResult[block];
                foreach (var (_, valueSet) in blockResult.Data)
                {
                    var analysisValues = valueSet.AnalysisValues;
                    if (analysisValues.Count > 1)
                    {
                        foreach (var value in analysisValues)
                        {
                            if (value is InvocationCountAbstractValue invocationCountAbstractValue)
                            {
                                context.ReportDiagnostic(invocationCountAbstractValue.InvocationOperation.CreateDiagnostic(MultipleEnumerableDescriptor));
                            }
                        }
                    }
                }
            }
        }

        private static GlobalFlowStateDataFlowOperationVisitor CreateOperationVisitor(
            GlobalFlowStateAnalysisContext context, ImmutableArray<IMethodSymbol> wellKnownLinqMethodsCausingEnumeration)
            => new InvocationCountDataFlowOperationVisitor(context, wellKnownLinqMethodsCausingEnumeration);

        private static ImmutableArray<IOperation> GetOperationsCausingEnumeration(
            IOperation root,
            ImmutableArray<IMethodSymbol> wellKnownLinqMethodsCausingEnumeration)
        {
            using var builder = ArrayBuilder<IOperation>.GetInstance();

            foreach (var operation in root.Children)
            {
                foreach (var descendantOperation in operation.Descendants())
                {
                    // 1. If the operation is Linq Method that causing Enumeration.
                    // e.g. ToArray() / First() ...
                    if (descendantOperation is IInvocationOperation invocationOperation
                        && wellKnownLinqMethodsCausingEnumeration.Contains(invocationOperation.TargetMethod))
                    {
                        // Find the argument that matches to the 'source' in the linq method.
                        // In most cases it is the first argument, but in cases like:
                        // Enumerable.Select(selector: i => i + 1, source: x), this would be the second argument
                        var sourceArgument = invocationOperation.Arguments.FirstOrDefault(arg => arg.Parameter.Name == "source");
                        if (sourceArgument is { Value: ILocalReferenceOperation or IParameterReferenceOperation })
                        {
                            builder.Add(descendantOperation);
                        }
                    }
                    // 2. For each loop
                    else if (descendantOperation is IForEachLoopOperation { Collection: IConversionOperation { Operand: ILocalReferenceOperation or IParameterInitializerOperation } collection } forEachLoopOperation)
                    {
                        builder.Add(collection.Operand);
                    }
                }
            }

            return builder.ToImmutable();
        }

        private static ImmutableArray<IMethodSymbol> GetWellKnownLinqMethodsCausingEnumeration(WellKnownTypeProvider wellKnownTypeProvider)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();

            if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqEnumerable, out var systemLinqEnumerableType))
            {
                foreach (var methodName in s_wellKnownLinqMethods)
                {
                    builder.AddRange(systemLinqEnumerableType.GetMembers(methodName).OfType<IMethodSymbol>());
                }
            }

            return builder.ToImmutable();
        }
    }
}
