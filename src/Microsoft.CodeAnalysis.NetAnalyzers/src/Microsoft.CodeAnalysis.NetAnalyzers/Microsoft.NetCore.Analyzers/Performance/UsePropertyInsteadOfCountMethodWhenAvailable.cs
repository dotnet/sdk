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
        private static readonly DiagnosticDescriptor s_rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

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
            private readonly Lazy<INamedTypeSymbol?> _immutableArrayType;
            private readonly Lazy<IPropertySymbol?> _iCollectionCountProperty;
            private readonly Lazy<INamedTypeSymbol?> _iCollectionOfType;

            /// <summary>
            /// Initializes a new instance of the <see cref="OperationActionsContext"/> class.
            /// </summary>
            /// <param name="compilation">The compilation.</param>
            /// <param name="enumerableType">Type of the enumerable.</param>
            public OperationActionsContext(Compilation compilation, INamedTypeSymbol enumerableType)
            {
                Compilation = compilation;
                EnumerableType = enumerableType;
                _immutableArrayType = new Lazy<INamedTypeSymbol?>(() => Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsImmutableImmutableArray1), true);
                _iCollectionCountProperty = new Lazy<IPropertySymbol?>(ResolveICollectionCountProperty, true);
                _iCollectionOfType = new Lazy<INamedTypeSymbol?>(() => Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericICollection1), true);
            }

            /// <summary>
            /// Gets the <see cref="Microsoft.CodeAnalysis.Compilation"/>.
            /// </summary>
            /// <value>The <see cref="Microsoft.CodeAnalysis.Compilation"/>.</value>
            internal Compilation Compilation { get; }

            private INamedTypeSymbol EnumerableType { get; }

            /// <summary>
            /// Gets the <see cref="System.Collections.ICollection.Count"/> property.
            /// </summary>
            /// <value>The <see cref="System.Collections.ICollection.Count"/> property.</value>
            private IPropertySymbol? ICollectionCountProperty => _iCollectionCountProperty.Value;

            /// <summary>
            /// Gets the type of the <see cref="System.Collections.Immutable.ImmutableArray{TSource}"/> type.
            /// </summary>
            /// <value>The <see cref="System.Collections.Immutable.ImmutableArray{TSource}"/> type.</value>
            private INamedTypeSymbol? ICollectionOfTType => _iCollectionOfType.Value;

            /// <summary>
            /// Gets the type of the <see cref="System.Collections.Generic.ICollection{TSource}"/> type.
            /// </summary>
            /// <value>The <see cref="System.Collections.Generic.ICollection{TSource}"/> type.</value>
            internal INamedTypeSymbol? ImmutableArrayType => _immutableArrayType.Value;

            /// <summary>
            /// Gets the type of the <see cref="System.Collections.ICollection.Count"/> property, if one and only one exists.
            /// </summary>
            /// <returns>The <see cref="System.Collections.ICollection.Count"/> property.</returns>
            private IPropertySymbol? ResolveICollectionCountProperty()
            {
                IPropertySymbol? countProperty = null;

                if (Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsICollection) is INamedTypeSymbol iCollectionType)
                {
                    foreach (var member in iCollectionType.GetMembers())
                    {
                        if (member is IPropertySymbol property && property.Name.Equals(CountPropertyName, StringComparison.Ordinal))
                        {
                            if (countProperty is null)
                            {
                                countProperty = property;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }
                }

                return countProperty;
            }

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
            /// Determines whether the specified invocation target implements <see cref="System.Collections.ICollection"/>.
            /// </summary>
            /// <param name="invocationTarget">The invocation target.</param>
            /// <returns><see langword="true" /> if the specified invocation target implements <see cref="System.Collections.ICollection"/>; otherwise, <see langword="false" />.</returns>
            internal bool IsICollectionImplementation(ITypeSymbol invocationTarget)
                => this.ICollectionCountProperty is object &&
                    invocationTarget.FindImplementationForInterfaceMember(this.ICollectionCountProperty) is IPropertySymbol countProperty &&
                    !countProperty.ExplicitInterfaceImplementations.Any();

            /// <summary>
            /// Determines whether the specified invocation target implements System.Collections.Generic.ICollection{TSource}.
            /// </summary>
            /// <param name="invocationTarget">The invocation target.</param>
            /// <returns><see langword="true" /> if the specified invocation target implements System.Collections.Generic.ICollection{TSource}; otherwise, <see langword="false" />.</returns>
            internal bool IsICollectionOfTImplementation(ITypeSymbol invocationTarget)
            {
                if (ICollectionOfTType is null)
                {
                    return false;
                }

                if (isCollectionOfTInterface(invocationTarget, ICollectionOfTType))
                {
                    return true;
                }

                if (invocationTarget.TypeKind == TypeKind.Interface)
                {
                    if (invocationTarget.GetMembers(CountPropertyName).OfType<IPropertySymbol>().Any())
                    {
                        return false;
                    }

                    foreach (var @interface in invocationTarget.AllInterfaces)
                    {
                        if (@interface.OriginalDefinition is INamedTypeSymbol originalInterfaceDefinition &&
                            isCollectionOfTInterface(originalInterfaceDefinition, ICollectionOfTType))
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    foreach (var @interface in invocationTarget.AllInterfaces)
                    {
                        if (@interface.OriginalDefinition is INamedTypeSymbol originalInterfaceDefinition &&
                            isCollectionOfTInterface(originalInterfaceDefinition, ICollectionOfTType))
                        {
                            if (invocationTarget.FindImplementationForInterfaceMember(@interface.GetMembers(CountPropertyName)[0]) is IPropertySymbol propertyImplementation &&
                                !propertyImplementation.ExplicitInterfaceImplementations.Any())
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;

                static bool isCollectionOfTInterface(ITypeSymbol type, ITypeSymbol iCollectionOfTType)
                    => iCollectionOfTType.Equals(type.OriginalDefinition);
            }

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

                if (Context.IsICollectionImplementation(invocationTarget) || Context.IsICollectionOfTImplementation(invocationTarget))
                {
                    return CountPropertyName;
                }

                return null;
            }
        }
    }
}
