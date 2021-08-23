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
    /// <summary>
    /// CA1826: Do not use Enumerable methods on indexable collections. Instead use the collection directly
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer : DiagnosticAnalyzer
    {
        private const string IReadOnlyListMetadataName = "System.Collections.Generic.IReadOnlyList`1";
        private const string IListMetadataName = "System.Collections.Generic.IList`1";
        private const string EnumerableMetadataName = "System.Linq.Enumerable";

        internal const string RuleId = "CA1826";

        internal const string MethodPropertyKey = "method";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.IdeSuggestion,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: false,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var listType = context.Compilation.GetOrCreateTypeByMetadataName(IListMetadataName);
            var readonlyListType = context.Compilation.GetOrCreateTypeByMetadataName(IReadOnlyListMetadataName);
            var enumerableType = context.Compilation.GetOrCreateTypeByMetadataName(EnumerableMetadataName);
            if (readonlyListType == null || enumerableType == null || listType == null)
            {
                return;
            }

            context.RegisterOperationAction(operationContext =>
            {
                var invocation = (IInvocationOperation)operationContext.Operation;

                var excludeOrDefaultMethods = operationContext.Options.GetBoolOptionValue(
                    EditorConfigOptionNames.ExcludeOrDefaultMethods, Rule, invocation.Syntax.SyntaxTree,
                    operationContext.Compilation, defaultValue: false);

                if (!IsPossibleLinqInvocation(invocation, excludeOrDefaultMethods))
                {
                    return;
                }

                var methodSymbol = invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod;
                var targetType = invocation.GetReceiverType(operationContext.Compilation, beforeConversion: true, cancellationToken: operationContext.CancellationToken);
                if (methodSymbol == null || targetType == null)
                {
                    return;
                }

                if (!IsSingleParameterLinqMethod(methodSymbol, enumerableType))
                {
                    return;
                }

                if (!IsTypeWithInefficientLinqMethods(targetType, readonlyListType, listType))
                {
                    return;
                }

                var properties = new Dictionary<string, string> { [MethodPropertyKey] = invocation.TargetMethod.Name }.ToImmutableDictionary();
                operationContext.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation(), properties));
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
        private static bool IsTypeWithInefficientLinqMethods(ITypeSymbol targetType, ITypeSymbol readonlyListType, ITypeSymbol listType)
        {
            // If this type is simply IReadOnlyList<T> then no further checking is needed.
            if (targetType.TypeKind == TypeKind.Interface && targetType.OriginalDefinition.Equals(readonlyListType))
            {
                return true;
            }

            bool implementsReadOnlyList = false;
            bool implementsList = false;
            foreach (var current in targetType.AllInterfaces)
            {
                if (current.OriginalDefinition.Equals(readonlyListType))
                {
                    implementsReadOnlyList = true;
                }

                if (current.OriginalDefinition.Equals(listType))
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

        private static bool IsPossibleLinqInvocation(IInvocationOperation invocation, bool excludeOrDefaultMethods)
        {
            return invocation.TargetMethod.Name switch
            {
                "Last" or "First" or "Count" => true,
                "LastOrDefault" or "FirstOrDefault" => !excludeOrDefaultMethods,
                _ => false,
            };
        }
    }
}