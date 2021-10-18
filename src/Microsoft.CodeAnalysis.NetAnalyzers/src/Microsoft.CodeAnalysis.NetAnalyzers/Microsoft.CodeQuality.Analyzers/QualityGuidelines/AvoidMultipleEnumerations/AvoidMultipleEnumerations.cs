// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;
using Microsoft.CodeAnalysis.Operations;

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

        internal abstract GlobalFlowStateValueSetFlowOperationVisitor CreateOperationVisitor(
            GlobalFlowStateAnalysisContext context,
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

            var analysisResult = GlobalFlowStateAnalysis.TryGetOrComputeResult(
                cfg,
                context.OwningSymbol,
                globalFlowStateAnalysisContext => CreateOperationVisitor(globalFlowStateAnalysisContext, wellKnownDelayExecutionMethods, wellKnownEnumerationMethods, getEnumeratorSymbol),
                wellKnownTypeProvider,
                context.Options,
                MultipleEnumerableDescriptor,
                performValueContentAnalysis: false,
                pessimisticAnalysis: false,
                out var valueContentAnalysisResult);

            if (analysisResult == null)
            {
                return;
            }

            foreach (var potentialDiagnosticOperation in potentialDiagnosticOperations)
            {
                var analysisValueSet = analysisResult[potentialDiagnosticOperation.Kind, potentialDiagnosticOperation.Syntax];
                if (potentialDiagnosticOperation is ILocalReferenceOperation localReferenceOperation)
                {
                    if (EnumerateTwice(localReferenceOperation.Local, analysisValueSet))
                    {
                        context.ReportDiagnostic(potentialDiagnosticOperation.CreateDiagnostic(MultipleEnumerableDescriptor));
                    }
                }
                else if (potentialDiagnosticOperation is IParameterReferenceOperation parameterReferenceOperation)
                {
                    if (EnumerateTwice(parameterReferenceOperation.Parameter, analysisValueSet))
                    {
                        context.ReportDiagnostic(potentialDiagnosticOperation.CreateDiagnostic(MultipleEnumerableDescriptor));
                    }
                }
            }
        }
    }
}
