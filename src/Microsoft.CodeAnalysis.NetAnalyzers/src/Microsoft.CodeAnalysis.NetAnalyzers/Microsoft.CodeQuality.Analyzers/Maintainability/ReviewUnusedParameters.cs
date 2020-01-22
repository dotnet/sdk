// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    using UnusedParameterDictionary = IDictionary<IMethodSymbol, ISet<IParameterSymbol>>;

    /// <summary>
    /// CA1801: Review unused parameters
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ReviewUnusedParametersAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1801";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ReviewUnusedParametersTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ReviewUnusedParametersMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ReviewUnusedParametersDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.Disabled,    // We have an implementation in IDE.
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

#pragma warning disable RS1026 // Enable concurrent execution
        public override void Initialize(AnalysisContext context)
#pragma warning restore RS1026 // Enable concurrent execution
        {
            // TODO: Consider making this analyzer thread-safe.
            //context.EnableConcurrentExecution();

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                INamedTypeSymbol? eventsArgSymbol = compilationStartContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEventArgs);

                // Ignore conditional methods (FxCop compat - One conditional will often call another conditional method as its only use of a parameter)
                INamedTypeSymbol? conditionalAttributeSymbol = compilationStartContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsConditionalAttribute);

                // Ignore methods with special serialization attributes (FxCop compat - All serialization methods need to take 'StreamingContext')
                INamedTypeSymbol? onDeserializingAttribute = compilationStartContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationOnDeserializingAttribute);
                INamedTypeSymbol? onDeserializedAttribute = compilationStartContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationOnDeserializedAttribute);
                INamedTypeSymbol? onSerializingAttribute = compilationStartContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationOnSerializingAttribute);
                INamedTypeSymbol? onSerializedAttribute = compilationStartContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationOnSerializedAttribute);
                INamedTypeSymbol? obsoleteAttribute = compilationStartContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemObsoleteAttribute);

                ImmutableHashSet<INamedTypeSymbol?> attributeSetForMethodsToIgnore = ImmutableHashSet.Create(
                    conditionalAttributeSymbol,
                    onDeserializedAttribute,
                    onDeserializingAttribute,
                    onSerializedAttribute,
                    onSerializingAttribute,
                    obsoleteAttribute);

                UnusedParameterDictionary unusedMethodParameters = new ConcurrentDictionary<IMethodSymbol, ISet<IParameterSymbol>>();
                ISet<IMethodSymbol> methodsUsedAsDelegates = new HashSet<IMethodSymbol>();

                // Create a list of functions to exclude from analysis. We assume that any function that is used in an IMethodBindingExpression
                // cannot have its signature changed, and add it to the list of methods to be excluded from analysis.
                compilationStartContext.RegisterOperationAction(operationContext =>
                {
                    var methodBinding = (IMethodReferenceOperation)operationContext.Operation;
                    methodsUsedAsDelegates.Add(methodBinding.Method.OriginalDefinition);
                }, OperationKind.MethodReference);

                compilationStartContext.RegisterOperationBlockStartAction(startOperationBlockContext =>
                {
                    // We only care about methods.
                    if (!(startOperationBlockContext.OwningSymbol is IMethodSymbol method))
                    {
                        return;
                    }

                    // Check to see if the method just throws a NotImplementedException/NotSupportedException
                    // We shouldn't warn about parameters in that case
                    if (startOperationBlockContext.IsMethodNotImplementedOrSupported())
                    {
                        return;
                    }

                    AnalyzeMethod(method, startOperationBlockContext, unusedMethodParameters,
                        eventsArgSymbol, methodsUsedAsDelegates, attributeSetForMethodsToIgnore);

                    foreach (var localFunctionOperation in startOperationBlockContext.OperationBlocks.SelectMany(o => o.Descendants()).OfType<ILocalFunctionOperation>())
                    {
                        AnalyzeMethod(localFunctionOperation.Symbol, startOperationBlockContext, unusedMethodParameters,
                           eventsArgSymbol, methodsUsedAsDelegates, attributeSetForMethodsToIgnore);
                    }
                });

                // Register a compilation end action to filter all methods used as delegates and report any diagnostics
                compilationStartContext.RegisterCompilationEndAction(compilationAnalysisContext =>
                {
                    // Report diagnostics for unused parameters.
                    var unusedParameters = unusedMethodParameters.Where(kvp => !methodsUsedAsDelegates.Contains(kvp.Key)).SelectMany(kvp => kvp.Value);
                    foreach (var parameter in unusedParameters)
                    {
                        var diagnostic = Diagnostic.Create(Rule, parameter.Locations[0], parameter.Name, parameter.ContainingSymbol.Name);
                        compilationAnalysisContext.ReportDiagnostic(diagnostic);
                    }

                });
            });
        }

        private static void AnalyzeMethod(
            IMethodSymbol method,
            OperationBlockStartAnalysisContext startOperationBlockContext,
            UnusedParameterDictionary unusedMethodParameters,
            INamedTypeSymbol? eventsArgSymbol,
            ISet<IMethodSymbol> methodsUsedAsDelegates,
            ImmutableHashSet<INamedTypeSymbol?> attributeSetForMethodsToIgnore)
        {
            // We only care about methods with parameters.
            if (method.Parameters.IsEmpty)
            {
                return;
            }

            // Ignore implicitly declared methods, extern methods, abstract methods, virtual methods, interface implementations and finalizers (FxCop compat).
            if (method.IsImplicitlyDeclared ||
                method.IsExtern ||
                method.IsAbstract ||
                method.IsVirtual ||
                method.IsOverride ||
                method.IsImplementationOfAnyInterfaceMember() ||
                method.IsFinalizer())
            {
                return;
            }

            // Ignore property accessors.
            if (method.IsPropertyAccessor())
            {
                return;
            }

            // Ignore event handler methods "Handler(object, MyEventArgs)"
            if (method.Parameters.Length == 2 &&
                method.Parameters[0].Type.SpecialType == SpecialType.System_Object &&
                // UWP has specific EventArgs not inheriting from System.EventArgs. It was decided to go for a suffix match rather than a whitelist.
                (method.Parameters[1].Type.Inherits(eventsArgSymbol) || method.Parameters[1].Type.Name.EndsWith("EventArgs", StringComparison.Ordinal)))
            {
                return;
            }

            // Ignore methods with any attributes in 'attributeSetForMethodsToIgnore'.
            if (method.GetAttributes().Any(a => a.AttributeClass != null && attributeSetForMethodsToIgnore.Contains(a.AttributeClass)))
            {
                return;
            }

            // Ignore methods that were used as delegates
            if (methodsUsedAsDelegates.Contains(method))
            {
                return;
            }

            // Bail out if user has configured to skip analysis for the method.
            if (!method.MatchesConfiguredVisibility(
                    startOperationBlockContext.Options,
                    Rule,
                    startOperationBlockContext.CancellationToken,
                    defaultRequiredVisibility: SymbolVisibilityGroup.All))
            {
                return;
            }

            // Initialize local mutable state in the start action.
            var analyzer = new UnusedParametersAnalyzer(method, unusedMethodParameters);

            // Register an intermediate non-end action that accesses and modifies the state.
            startOperationBlockContext.RegisterOperationAction(analyzer.AnalyzeParameterReference, OperationKind.ParameterReference);

            // Register an end action to add unused parameters to the unusedMethodParameters dictionary
            startOperationBlockContext.RegisterOperationBlockEndAction(analyzer.OperationBlockEndAction);
        }

        private class UnusedParametersAnalyzer
        {
            #region Per-CodeBlock mutable state

            private readonly HashSet<IParameterSymbol> _unusedParameters;
            private readonly UnusedParameterDictionary _finalUnusedParameters;
            private readonly IMethodSymbol _method;

            #endregion

            #region State intialization

            public UnusedParametersAnalyzer(IMethodSymbol method, UnusedParameterDictionary finalUnusedParameters)
            {
                // Initialization: Assume all parameters are unused, except the ones with special discard name.
                _unusedParameters = new HashSet<IParameterSymbol>(method.Parameters.Where(p => !p.IsSymbolWithSpecialDiscardName()));
                _finalUnusedParameters = finalUnusedParameters;
                _method = method;
            }

            #endregion

            #region Intermediate actions

            public void AnalyzeParameterReference(OperationAnalysisContext context)
            {
                // Check if we have any pending unreferenced parameters.
                if (_unusedParameters.Count == 0)
                {
                    return;
                }

                // Mark this parameter as used.
                IParameterSymbol parameter = ((IParameterReferenceOperation)context.Operation).Parameter;
                _unusedParameters.Remove(parameter);
            }

            #endregion

            #region End action

            public void OperationBlockEndAction(OperationBlockAnalysisContext _)
            {
                // Do not raise warning for unused 'this' parameter of an extension method.
                if (_method.IsExtensionMethod)
                {
                    var thisParamter = _unusedParameters.Where(p => p.Ordinal == 0).FirstOrDefault();
                    _unusedParameters.Remove(thisParamter);
                }

                _finalUnusedParameters.Add(_method, _unusedParameters);
            }

            #endregion
        }
    }
}