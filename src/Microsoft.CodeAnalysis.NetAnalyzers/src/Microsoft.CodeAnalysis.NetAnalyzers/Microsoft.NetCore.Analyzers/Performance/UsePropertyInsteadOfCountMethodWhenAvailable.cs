// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    /// <summary>
    /// CA1829: Use property instead of <see cref="Enumerable.Count{TSource}(System.Collections.Generic.IEnumerable{TSource})"/>, when available.
    /// Implements the <see cref="DiagnosticAnalyzer" />
    /// </summary>
    /// <remarks>
    /// Flags the use of <see cref="Enumerable.Count{TSource}(System.Collections.Generic.IEnumerable{TSource})"/> on types that are know to have a property with the same semantics:
    /// <c>Length</c>, <c>Count</c>.
    /// </remarks>
    public abstract class UsePropertyInsteadOfCountMethodWhenAvailableAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1829";
        internal const string PropertyNameKey = nameof(PropertyNameKey);
        private const string CountPropertyName = "Count";
        private const string LengthPropertyName = "Length";
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UsePropertyInsteadOfCountMethodWhenAvailableTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UsePropertyInsteadOfCountMethodWhenAvailableMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UsePropertyInsteadOfCountMethodWhenAvailableDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Performance,
            DiagnosticHelpers.DefaultDiagnosticSeverity,
            isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultForVsixAndNuget,
            description: s_localizableDescription,
#pragma warning disable CA1308 // Normalize strings to uppercase
            helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/" + RuleId.ToLowerInvariant());
#pragma warning restore CA1308 // Normalize strings to uppercase

        /// <summary>
        /// Returns a set of descriptors for the diagnostics that this analyzer is capable of producing.
        /// </summary>
        /// <value>The supported diagnostics.</value>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(s_rule);

        /// <summary>
        /// Called once at session start to register actions in the analysis context.
        /// </summary>
        /// <param name="context">The context.</param>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        /// <summary>
        /// Called on compilation start.
        /// </summary>
        /// <param name="context">The context.</param>
        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqEnumerable) is INamedTypeSymbol enumerableType)
            {
                var operationActionsContext = new OperationActionsContext(
                    context.Compilation,
                    enumerableType);

                context.RegisterOperationAction(
                    CreateOperationActionsHandler(operationActionsContext).AnalyzeInvocationOperation,
                    OperationKind.Invocation);
            }
        }

        /// <summary>
        /// Creates the operation actions handler.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The operation actions handler.</returns>
        protected abstract OperationActionsHandler CreateOperationActionsHandler(OperationActionsContext context);

        /// <summary>
        /// Holds the <see cref="Microsoft.CodeAnalysis.Compilation"/> and helper methods.
        /// </summary>
        protected sealed class OperationActionsContext
        {
            private readonly Lazy<WellKnownTypeProvider> _wellKnownTypeProvider;
            private readonly Lazy<INamedTypeSymbol?> _immutableArrayType;

            /// <summary>
            /// Initializes a new instance of the <see cref="OperationActionsContext"/> class.
            /// </summary>
            /// <param name="compilation">The compilation.</param>
            /// <param name="enumerableType">Type of the enumerable.</param>
            public OperationActionsContext(Compilation compilation, INamedTypeSymbol enumerableType)
            {
                EnumerableType = enumerableType;
                _wellKnownTypeProvider = new Lazy<WellKnownTypeProvider>(() => WellKnownTypeProvider.GetOrCreate(compilation), true);
                _immutableArrayType = new Lazy<INamedTypeSymbol?>(() => _wellKnownTypeProvider.Value.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsImmutableImmutableArray), true);
            }

            private INamedTypeSymbol EnumerableType { get; }

            internal WellKnownTypeProvider WellKnownTypeProvider => _wellKnownTypeProvider.Value;

            /// <summary>
            /// Gets the type of the <see cref="System.Collections.Generic.ICollection{TSource}"/> type.
            /// </summary>
            /// <value>The <see cref="System.Collections.Generic.ICollection{TSource}"/> type.</value>
            internal INamedTypeSymbol? ImmutableArrayType => _immutableArrayType.Value;

            /// <summary>
            /// Determines whether the specified type symbol is the immutable array generic type.
            /// </summary>
            /// <param name="typeSymbol">The type symbol.</param>
            /// <returns><see langword="true" /> if the specified type symbol is the immutable array generic type; otherwise, <see langword="false" />.</returns>
            internal bool IsImmutableArrayType(ITypeSymbol typeSymbol)
                => this.ImmutableArrayType is object &&
                    typeSymbol is INamedTypeSymbol namedTypeSymbol &&
                    namedTypeSymbol.ConstructedFrom is INamedTypeSymbol constructedFrom &&
                    constructedFrom.Equals(this.ImmutableArrayType);

            /// <summary>
            /// Determines whether [is enumerable type] [the specified symbol].
            /// </summary>
            /// <param name="symbol">The symbol.</param>
            /// <returns><see langword="true" /> if [is enumerable type] [the specified symbol]; otherwise, <see langword="false" />.</returns>
            internal bool IsEnumerableType(ISymbol symbol)
                => this.EnumerableType.Equals(symbol);
        }

        /// <summary>
        /// Handler for operaction actions.
        /// </summary>
        protected abstract class OperationActionsHandler
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="OperationActionsHandler"/> class.
            /// </summary>
            /// <param name="context">The context.</param>
            protected OperationActionsHandler(OperationActionsContext context)
            {
                Context = context;
            }

            /// <summary>
            /// Gets the context.
            /// </summary>
            /// <value>The context.</value>
            protected OperationActionsContext Context { get; }

            internal void AnalyzeInvocationOperation(OperationAnalysisContext context)
            {
                var invocationOperation = (IInvocationOperation)context.Operation;

                if (GetEnumerableCountInvocationTargetType(invocationOperation) is ITypeSymbol invocationTarget &&
                    GetReplacementProperty(invocationTarget) is string propertyName)
                {
                    var propertiesBuilder = ImmutableDictionary.CreateBuilder<string, string>();
                    propertiesBuilder.Add(PropertyNameKey, propertyName);

                    var diagnostic = Diagnostic.Create(
                        s_rule,
                        invocationOperation.Syntax.GetLocation(),
                        propertiesBuilder.ToImmutable(),
                        propertyName);

                    context.ReportDiagnostic(diagnostic);
                }
            }

            /// <summary>
            /// Gets the type of the receiver of the <see cref="Enumerable.Count{TSource}(System.Collections.Generic.IEnumerable{TSource})"/>.
            /// </summary>
            /// <param name="invocationOperation">The invocation operation.</param>
            /// <returns>The <see cref="ITypeSymbol"/> of the receiver of the extension method.</returns>
            protected abstract ITypeSymbol? GetEnumerableCountInvocationTargetType(IInvocationOperation invocationOperation);

            /// <summary>
            /// Gets the replacement property.
            /// </summary>
            /// <param name="invocationTarget">The invocation target.</param>
            /// <returns>The name of the replacement property.</returns>
            private string? GetReplacementProperty(ITypeSymbol invocationTarget)
            {
                if ((invocationTarget.TypeKind == TypeKind.Array) || Context.IsImmutableArrayType(invocationTarget))
                {
                    return LengthPropertyName;
                }

                if (invocationTarget.HasAnyCollectionCountProperty(Context.WellKnownTypeProvider))
                {
                    return CountPropertyName;
                }

                return null;
            }
        }
    }
}
