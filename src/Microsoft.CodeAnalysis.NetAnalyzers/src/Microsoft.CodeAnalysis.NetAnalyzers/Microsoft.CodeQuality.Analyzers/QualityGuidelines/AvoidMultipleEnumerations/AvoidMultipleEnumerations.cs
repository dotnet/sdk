// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Composition;
using System.Linq;
using System.Security.Claims;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;

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

        private static readonly ImmutableArray<string> s_wellKnownDelayExecutionLinqMethod = ImmutableArray<string>.Empty;

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(CompilationStartAction);
        }

        private static void CompilationStartAction(CompilationStartAnalysisContext context)
        {
            context.RegisterOperationBlockStartAction(OnOperationBlockStart);
        }

        private static void OnOperationBlockStart(OperationBlockStartAnalysisContext context)
        {
            using var potentialDiagnosticOperationsBuilder = PooledHashSet<IOperation>.GetInstance();
            context.RegisterOperationAction(
                context => CollectPotentialDiagnosticOperations(context, potentialDiagnosticOperationsBuilder),
                OperationKind.ParameterReference,
                OperationKind.LocalReference);

            var potentialDiagnosticOperations = potentialDiagnosticOperationsBuilder.ToImmutable();
            if (potentialDiagnosticOperations.IsEmpty)
            {
                return;
            }

            context.RegisterOperationBlockEndAction(context => Analyze(context, potentialDiagnosticOperations));
        }

        private static void CollectPotentialDiagnosticOperations(OperationAnalysisContext context, PooledHashSet<IOperation> builder)
        {
            var operation = context.Operation;
            var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
            var wellKnownEnumerationMethods = GetWellKnownEnumerationMethods(wellKnownTypeProvider);
            var wellKnownDelayExecutionMethods = GetWellKnownDelayExecutionMethod(wellKnownTypeProvider);

            if (operation is IParameterReferenceOperation { Type: { SpecialType: SpecialType.System_Collections_Generic_IEnumerable_T } }
                or ILocalReferenceOperation { Type: { SpecialType: SpecialType.System_Collections_Generic_IEnumerable_T } }
                && IsParameterOrLocalEnumerated(operation, wellKnownDelayExecutingMethods: wellKnownDelayExecutionMethods, wellKnownEnumerationMethods: wellKnownEnumerationMethods))
            {
                builder.Add(operation);
            }
        }

        private static void Analyze(OperationBlockAnalysisContext context, ImmutableHashSet<IOperation> potentialDiagnosticOperations)
        {
            var cfg = context.OperationBlocks.GetControlFlowGraph();
            if (cfg == null)
            {
                return;
            }

            var basicBlocks = cfg.Blocks;
            var analysisResult = GlobalFlowStateAnalysis.TryGetOrComputeResult(
                cfg,
                context.OwningSymbol,
                globalFlowStateAnalysisContext => CreateOperationVisitor(globalFlowStateAnalysisContext, wellKnownEnumerationLinqMethods),
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
                            if (value is EnumerationInvocationAnalysisValue invocationCountAbstractValue)
                            {
                                context.ReportDiagnostic(invocationCountAbstractValue.InvocationOperation.CreateDiagnostic(MultipleEnumerableDescriptor));
                            }
                        }
                    }
                }
            }
        }

        private static GlobalFlowStateDataFlowOperationVisitor CreateOperationVisitor(
            GlobalFlowStateAnalysisContext context, ImmutableArray<IMethodSymbol> wellKnownEnumerationLinqMethods)
            => new InvocationCountDataFlowOperationVisitor(context, wellKnownEnumerationLinqMethods);

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

        private static ImmutableArray<IMethodSymbol> GetWellKnownEnumerationMethods(WellKnownTypeProvider wellKnownTypeProvider)
            => GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, s_wellKnownDelayExecutionLinqMethod);

        private static ImmutableArray<IMethodSymbol> GetWellKnownDelayExecutionMethod(WellKnownTypeProvider wellKnownTypeProvider)
            => GetWellKnownMethods(wellKnownTypeProvider, WellKnownTypeNames.SystemLinqEnumerable, s_wellKnownDelayExecutionLinqMethod);

        private static ImmutableArray<IMethodSymbol> GetWellKnownMethods(WellKnownTypeProvider wellKnownTypeProvider, string typeName, ImmutableArray<string> methodNames)
        {
            using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
            if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(typeName, out var type))
            {
                foreach (var methodName in methodNames)
                {
                    builder.AddRange(type.GetMembers(methodName).OfType<IMethodSymbol>());
                }
            }

            return builder.ToImmutable();
        }
    }
}
