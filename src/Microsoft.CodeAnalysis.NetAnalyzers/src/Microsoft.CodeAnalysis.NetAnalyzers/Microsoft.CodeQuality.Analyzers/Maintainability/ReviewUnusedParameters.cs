// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    /// <summary>
    /// CA1801: Review unused parameters
    /// </summary>
    public abstract class ReviewUnusedParametersAnalyzer : DiagnosticAnalyzer
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

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartContext.Compilation);

                INamedTypeSymbol? eventsArgSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEventArgs);

                // Ignore conditional methods (FxCop compat - One conditional will often call another conditional method as its only use of a parameter)
                INamedTypeSymbol? conditionalAttributeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsConditionalAttribute);

                // Ignore methods with special serialization attributes (FxCop compat - All serialization methods need to take 'StreamingContext')
                INamedTypeSymbol? onDeserializingAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationOnDeserializingAttribute);
                INamedTypeSymbol? onDeserializedAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationOnDeserializedAttribute);
                INamedTypeSymbol? onSerializingAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationOnSerializingAttribute);
                INamedTypeSymbol? onSerializedAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationOnSerializedAttribute);
                INamedTypeSymbol? obsoleteAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemObsoleteAttribute);

                INamedTypeSymbol? serializationInfoType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationSerializationInfo);
                INamedTypeSymbol? streamingContextType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationStreamingContext);

                ImmutableHashSet<INamedTypeSymbol?> attributeSetForMethodsToIgnore = ImmutableHashSet.Create(
                    conditionalAttributeSymbol,
                    onDeserializedAttribute,
                    onDeserializingAttribute,
                    onSerializedAttribute,
                    onSerializingAttribute,
                    obsoleteAttribute);

                compilationStartContext.RegisterSymbolStartAction(symbolStartContext =>
                {
                    // Map from parameter to a bool indicating if the parameter is used or not.
                    var parameterUsageMap = new ConcurrentDictionary<IParameterSymbol, bool>();

                    // Set of methods which are used as delegates.
                    var methodsUsedAsDelegates = new ConcurrentDictionary<IMethodSymbol, bool>();

                    // Add candidate parameters for methods.
                    symbolStartContext.RegisterOperationBlockStartAction(startOperationBlockContext =>
                    {
                        if (startOperationBlockContext.OwningSymbol is IMethodSymbol method &&
                            ShouldAnalyzeMethod(method, startOperationBlockContext, eventsArgSymbol, attributeSetForMethodsToIgnore, serializationInfoType, streamingContextType))
                        {
                            AddParameters(method, parameterUsageMap);
                        }
                    });

                    // Add candidate parameters for local functions.
                    symbolStartContext.RegisterOperationAction(
                        context => AddParameters(((ILocalFunctionOperation)context.Operation).Symbol, parameterUsageMap),
                        OperationKind.LocalFunction);

                    // Add methods used as delegates.
                    symbolStartContext.RegisterOperationAction(
                        context => methodsUsedAsDelegates.TryAdd(((IMethodReferenceOperation)context.Operation).Method.OriginalDefinition, true),
                        OperationKind.MethodReference);

                    // Mark parameters with a parameter reference as used.
                    symbolStartContext.RegisterOperationAction(
                        context => parameterUsageMap.AddOrUpdate(
                            ((IParameterReferenceOperation)context.Operation).Parameter,
                            addValue: true,
                            updateValueFactory: ReturnTrue),
                        OperationKind.ParameterReference);

                    // Report unused parameters in SymbolEnd action.
                    symbolStartContext.RegisterSymbolEndAction(
                        context => ReportUnusedParameters(context, parameterUsageMap, methodsUsedAsDelegates));
                }, SymbolKind.NamedType);
            });
        }

        private static bool ReturnTrue(IParameterSymbol param, bool value) => true;

        private static void AddParameters(IMethodSymbol method, ConcurrentDictionary<IParameterSymbol, bool> unusedParameters)
        {
            foreach (var parameter in method.Parameters)
            {
                unusedParameters.TryAdd(parameter, false);
            }
        }

        private static void ReportUnusedParameters(
            SymbolAnalysisContext symbolEndContext,
            ConcurrentDictionary<IParameterSymbol, bool> parameterUsageMap,
            ConcurrentDictionary<IMethodSymbol, bool> methodsUsedAsDelegates)
        {
            // Report diagnostics for unused parameters.
            foreach (var (parameter, used) in parameterUsageMap)
            {
                if (used || parameter.Name.Length == 0)
                {
                    continue;
                }

                var containingMethod = (IMethodSymbol)parameter.ContainingSymbol;

                // Don't report parameters for methods used as delegates.
                // We assume these methods have signature requirements,
                // and hence its unused parameters cannot be removed.
                if (methodsUsedAsDelegates.ContainsKey(containingMethod))
                {
                    continue;
                }

                // Do not flag unused 'this' parameter of an extension method.
                if (containingMethod.IsExtensionMethod && parameter.Ordinal == 0)
                {
                    continue;
                }

                // Do not flag unused parameters with special discard symbol name.
                if (parameter.IsSymbolWithSpecialDiscardName())
                {
                    continue;
                }

                var diagnostic = parameter.CreateDiagnostic(Rule, parameter.Name, parameter.ContainingSymbol.Name);
                symbolEndContext.ReportDiagnostic(diagnostic);
            }
        }

#pragma warning disable RS1012 // Start action has no registered actions.
        private bool ShouldAnalyzeMethod(
            IMethodSymbol method,
            OperationBlockStartAnalysisContext startOperationBlockContext,
            INamedTypeSymbol? eventsArgSymbol,
            ImmutableHashSet<INamedTypeSymbol?> attributeSetForMethodsToIgnore,
            INamedTypeSymbol? serializationInfoType,
            INamedTypeSymbol? streamingContextType)
#pragma warning restore RS1012 // Start action has no registered actions.
        {
            // We only care about methods with parameters.
            if (method.Parameters.IsEmpty)
            {
                return false;
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
                return false;
            }

            // Ignore property accessors.
            if (method.IsPropertyAccessor())
            {
                return false;
            }

            // Ignore primary constructor (body-less) of positional records.
            if (IsPositionalRecordPrimaryConstructor(method))
            {
                return false;
            }

            // Ignore serialization special methods
            if (method.IsSerializationConstructor(serializationInfoType, streamingContextType) ||
                method.IsGetObjectData(serializationInfoType, streamingContextType))
            {
                return false;
            }

            // Ignore event handler methods "Handler(object, MyEventArgs)"
            if (method.HasEventHandlerSignature(eventsArgSymbol))
            {
                return false;
            }

            // Ignore methods with any attributes in 'attributeSetForMethodsToIgnore'.
            if (method.GetAttributes().Any(a => a.AttributeClass != null && attributeSetForMethodsToIgnore.Contains(a.AttributeClass)))
            {
                return false;
            }

            // Bail out if user has configured to skip analysis for the method.
            if (!startOperationBlockContext.Options.MatchesConfiguredVisibility(
                Rule,
                method,
                startOperationBlockContext.Compilation,
                defaultRequiredVisibility: SymbolVisibilityGroup.All))
            {
                return false;
            }

            // Check to see if the method just throws a NotImplementedException/NotSupportedException
            // We shouldn't warn about parameters in that case
            if (startOperationBlockContext.IsMethodNotImplementedOrSupported())
            {
                return false;
            }

            // Ignore generated method for top level statements
            if (method.IsTopLevelStatementsEntryPointMethod())
            {
                return false;
            }

            return true;
        }

        protected abstract bool IsPositionalRecordPrimaryConstructor(IMethodSymbol methodSymbol);
    }
}
