// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1826: <inheritdoc cref="DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1826";

        internal const string MethodPropertyKey = "method";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyTitle)),
            CreateLocalizableResourceString(nameof(DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyDescription)),
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
            var enumerableType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqEnumerable);
            if (enumerableType == null)
            {
                return;
            }

            context.RegisterOperationAction(context =>
            {
                var invocation = (IInvocationOperation)context.Operation;

                if (!IsPossibleLinqInvocation(invocation, context))
                {
                    return;
                }

                var methodSymbol = invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod;
                var targetType = invocation.GetReceiverType(context.Compilation, beforeConversion: true, cancellationToken: context.CancellationToken);
                if (methodSymbol == null || targetType == null)
                {
                    return;
                }

                if (!IsSingleParameterLinqMethod(methodSymbol, enumerableType))
                {
                    return;
                }

                if (!IsTypeWithInefficientLinqMethods(targetType))
                {
                    return;
                }

                var properties = new Dictionary<string, string?> { [MethodPropertyKey] = invocation.TargetMethod.Name }.ToImmutableDictionary();
                context.ReportDiagnostic(invocation.CreateDiagnostic(Rule, properties));
            }, OperationKind.Invocation);
        }

        /// <summary>
        /// The Enumerable.Last method will only special case indexable types that implement <see cref="IList{T}" />.  Types
        /// which implement only <see cref="IReadOnlyList{T}"/> will be treated the same as IEnumerable{T} and go through a
        /// full enumeration.  This method identifies such types.
        ///
        /// At this point it only identifies <see cref="IReadOnlyList{T}"/> directly but could easily be extended to support
        /// any type which has an index and count property.
        /// </summary>
        private static bool IsTypeWithInefficientLinqMethods(ITypeSymbol targetType)
        {
            // If this type is simply IReadOnlyList<T> then no further checking is needed.
            if (targetType.TypeKind == TypeKind.Interface && targetType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IReadOnlyList_T)
            {
                return true;
            }

            bool implementsReadOnlyList = false;
            bool implementsList = false;
            foreach (var current in targetType.AllInterfaces)
            {
                if (current.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IReadOnlyList_T)
                {
                    implementsReadOnlyList = true;
                }

                if (current.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IList_T)
                {
                    implementsList = true;
                }
            }

            return implementsReadOnlyList && !implementsList;
        }

        /// <summary>
        /// Is this a method on <see cref="Enumerable" /> which takes only a single parameter?
        /// </summary>
        /// <remarks>
        /// Many of the methods we target, like Last, have overloads that take a filter delegate.  It is
        /// completely appropriate to use such methods even with <see cref="IReadOnlyList{T}" />.  Only the single parameter
        /// ones are suspect
        /// </remarks>
        private static bool IsSingleParameterLinqMethod(IMethodSymbol methodSymbol, ITypeSymbol enumerableType)
        {
            Debug.Assert(methodSymbol.ReducedFrom == null);
            return
                methodSymbol.ContainingSymbol.Equals(enumerableType) &&
                methodSymbol.Parameters.Length == 1;
        }

        private static bool IsPossibleLinqInvocation(IInvocationOperation invocation, OperationAnalysisContext context)
        {
            return invocation.TargetMethod.Name switch
            {
                "Last" or "First" or "Count" => true,
                "LastOrDefault" or "FirstOrDefault" => !ShouldExcludeOrDefaultMethods(context),
                _ => false,
            };
        }

        private static bool ShouldExcludeOrDefaultMethods(OperationAnalysisContext context)
            => context.Options.GetBoolOptionValue(
                EditorConfigOptionNames.ExcludeOrDefaultMethods, Rule, context.Operation.Syntax.SyntaxTree,
                context.Compilation, defaultValue: false);
    }
}
