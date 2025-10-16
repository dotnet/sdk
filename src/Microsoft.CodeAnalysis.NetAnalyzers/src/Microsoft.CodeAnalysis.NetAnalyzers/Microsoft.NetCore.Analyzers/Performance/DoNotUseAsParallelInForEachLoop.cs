// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1876: <inheritdoc cref="DoNotUseAsParallelInForEachLoopTitle"/>
    /// Analyzer to detect misuse of AsParallel() when used directly in a foreach loop.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseAsParallelInForEachLoopAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1876";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotUseAsParallelInForEachLoopTitle)),
            CreateLocalizableResourceString(nameof(DoNotUseAsParallelInForEachLoopMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(DoNotUseAsParallelInForEachLoopDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var typeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);

            // Get the ParallelEnumerable type
            var parallelEnumerableType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqParallelEnumerable);

            if (parallelEnumerableType == null)
            {
                return;
            }

            // Get all AsParallel methods
            var asParallelMethods = ImmutableHashSet.CreateRange(
                parallelEnumerableType.GetMembers("AsParallel").OfType<IMethodSymbol>());

            if (asParallelMethods.IsEmpty)
            {
                return;
            }

            context.RegisterOperationAction(ctx => AnalyzeForEachLoop(ctx, asParallelMethods), OperationKind.Loop);
        }

        private static void AnalyzeForEachLoop(OperationAnalysisContext context, ImmutableHashSet<IMethodSymbol> asParallelMethods)
        {
            if (context.Operation is not IForEachLoopOperation forEachLoop)
            {
                return;
            }

            // Check if the collection is a direct result of AsParallel()
            var collection = forEachLoop.Collection;

            // Walk up conversions to find the actual operation
            while (collection is IConversionOperation conversion)
            {
                collection = conversion.Operand;
            }

            // Check if this is an invocation of AsParallel
            if (collection is IInvocationOperation invocation)
            {
                var targetMethod = invocation.TargetMethod;

                // For extension methods, we need to check the ReducedFrom or the original method
                var methodToCheck = targetMethod.ReducedFrom ?? targetMethod;

                if (asParallelMethods.Contains(methodToCheck.OriginalDefinition))
                {
                    // Report diagnostic on the AsParallel call
                    context.ReportDiagnostic(invocation.CreateDiagnostic(Rule));
                }
            }
        }
    }
}
