// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.AnalyzerUtilities.FlowAnalysis.Analysis.InvocationCountAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidMultipleEnumerations : DiagnosticAnalyzer
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

        private static readonly ImmutableArray<string> s_immediateExecutedMethods = ImmutableArray.Create(
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

            using var blockToTargetInvocationOperationsMapBuilder = PooledDictionary<BasicBlock, ImmutableArray<IInvocationOperation>>.GetInstance();
            var basicBlocks = cfg.Blocks;
            foreach (var block in basicBlocks)
            {
                if (!block.IsReachable || block.Operations.IsEmpty)
                {
                    continue;
                }

                using var arrayBuilder = ArrayBuilder<IInvocationOperation>.GetInstance();
                foreach (var operation in block.Operations)
                {
                    var immediateExecutedInvocationOperations = GetAllImmediateExecutedInvocationOperations(operation);
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
            var wellKnowTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
            var result2 = GlobalFlowStateAnalysis.TryGetOrComputeResult(
                cfg,
                context.OwningSymbol,
                CreateOperationVisitor,
                wellKnowTypeProvider,
                context.Options,
                MultipleEnumerableDescriptor,
                performValueContentAnalysis: true,
                pessimisticAnalysis: false,
                out var valueContentAnalysisResult);

            var result = InvocationCountAnalysis.TryGetOrComputeResult(
                cfg,
                context.OwningSymbol,
                wellKnowTypeProvider,
                context.Options,
                MultipleEnumerableDescriptor,
                pessimisticAnalysis: false,
                trackingMethodNames: s_immediateExecutedMethods);
            if (result == null)
            {
                return;
            }

            foreach (var (_, invocationOperations) in blockToTargetInvocationOperationsMap)
            {
                // TODO: Test IEnumerable[]
                var multipleEnumerationTargetSet = invocationOperations.WhereAsArray(InvokedMoreThanOneTime)
                    .Select(op => op.Arguments[0].Parameter)
                    .ToImmutableHashSet();
                foreach (var operation in invocationOperations)
                {
                    if (multipleEnumerationTargetSet.Contains(operation.Arguments[0].Parameter))
                    {
                        context.ReportDiagnostic(operation.Arguments[0].CreateDiagnostic(MultipleEnumerableDescriptor));
                    }
                }
            }

            // TODO: Find the Overload so that this can be static
            bool InvokedMoreThanOneTime(IInvocationOperation invocationOperation)
            {
                var arguments = invocationOperation.Arguments;
                if (!arguments.IsEmpty)
                {
                    var count = result[invocationOperation.Kind, invocationOperation.Syntax];
                    return count.InvocationTimes == InvocationTimes.MoreThanOneTime;
                }

                return false;
            }
        }

        private static GlobalFlowStateDataFlowOperationVisitor CreateOperationVisitor(GlobalFlowStateAnalysisContext context)
            => new InvocationCountDataFlowOperationVisitor(context);

        private static ImmutableArray<IInvocationOperation> GetAllImmediateExecutedInvocationOperations(IOperation root)
            => root.Descendants().OfType<IInvocationOperation>().WhereAsArray(IsImmediateExecutedLinqOperation);

        private static bool IsImmediateExecutedLinqOperation(IInvocationOperation operation)
        {
            var targetMethod = operation.TargetMethod;
            var arguments = operation.Arguments;

            return targetMethod.IsExtensionMethod
                   && s_immediateExecutedMethods.Contains(targetMethod.ToDisplayString(InvocationCountAnalysis.MethodFullyQualifiedNameFormat))
                   && !arguments.IsEmpty
                   && IsLocalIEnumerableOperation(arguments[0]);
        }

        private static bool IsLocalIEnumerableOperation(IArgumentOperation argumentOperation)
        {
            var value = argumentOperation.Value;
            return value is ILocalReferenceOperation or IParameterReferenceOperation && value.Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;
        }
    }
}
