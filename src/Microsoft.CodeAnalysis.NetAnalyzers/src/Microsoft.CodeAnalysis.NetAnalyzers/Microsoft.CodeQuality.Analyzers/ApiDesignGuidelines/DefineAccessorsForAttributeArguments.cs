// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1019: Define accessors for attribute arguments
    ///
    /// Cause:
    /// In its constructor, an attribute defines arguments that do not have corresponding properties.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DefineAccessorsForAttributeArgumentsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1019";
        internal const string AddAccessorCase = "AddAccessor";
        internal const string MakePublicCase = "MakePublic";
        internal const string RemoveSetterCase = "RemoveSetter";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DefineAccessorsForAttributeArgumentsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_defaultRuleMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DefineAccessorsForAttributeArgumentsMessageDefault), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_increaseVisibilityMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DefineAccessorsForAttributeArgumentsMessageIncreaseVisibility), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_removeSetterMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DefineAccessorsForAttributeArgumentsMessageRemoveSetter), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor DefaultRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                    s_localizableTitle,
                                                                                    s_defaultRuleMessage,
                                                                                    DiagnosticCategory.Design,
                                                                                    RuleLevel.Disabled,
                                                                                    description: null,
                                                                                    isPortedFxCopRule: true,
                                                                                    isDataflowRule: false);

        internal static DiagnosticDescriptor IncreaseVisibilityRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                               s_localizableTitle,
                                                                                               s_increaseVisibilityMessage,
                                                                                               DiagnosticCategory.Design,
                                                                                               RuleLevel.Disabled,
                                                                                               description: null,
                                                                                               isPortedFxCopRule: true,
                                                                                               isDataflowRule: false);

        internal static DiagnosticDescriptor RemoveSetterRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                         s_localizableTitle,
                                                                                         s_removeSetterMessage,
                                                                                         DiagnosticCategory.Design,
                                                                                         RuleLevel.Disabled,
                                                                                         description: null,
                                                                                         isPortedFxCopRule: true,
                                                                                         isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DefaultRule, IncreaseVisibilityRule, RemoveSetterRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                INamedTypeSymbol? attributeType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttribute);
                if (attributeType == null)
                {
                    return;
                }

                compilationContext.RegisterSymbolAction(context =>
                {
                    AnalyzeSymbol((INamedTypeSymbol)context.Symbol, attributeType, context.Compilation, context.ReportDiagnostic);
                },
                SymbolKind.NamedType);
            });
        }

        private static void AnalyzeSymbol(INamedTypeSymbol symbol, INamedTypeSymbol attributeType, Compilation compilation, Action<Diagnostic> addDiagnostic)
        {
            if (symbol != null && symbol.GetBaseTypesAndThis().Contains(attributeType) && symbol.DeclaredAccessibility != Accessibility.Private)
            {
                IEnumerable<IParameterSymbol> parametersToCheck = GetAllPublicConstructorParameters(symbol);
                if (parametersToCheck.Any())
                {
                    IDictionary<string, IPropertySymbol> propertiesMap = GetAllPropertiesInTypeChain(symbol);
                    AnalyzeParameters(compilation, parametersToCheck, propertiesMap, symbol, addDiagnostic);
                }
            }
        }

        private static IEnumerable<IParameterSymbol> GetAllPublicConstructorParameters(INamedTypeSymbol attributeType)
        {
            // FxCop compatibility:
            // Only examine parameters of public constructors. Can't use protected
            // constructors to define an attribute so this rule only applies to
            // public constructors.
            IEnumerable<IMethodSymbol> instanceConstructorsToCheck = attributeType.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public);

            if (instanceConstructorsToCheck.Any())
            {
                var uniqueParamNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (IMethodSymbol constructor in instanceConstructorsToCheck)
                {
                    foreach (IParameterSymbol parameter in constructor.Parameters)
                    {
                        if (uniqueParamNames.Add(parameter.Name))
                        {
                            yield return parameter;
                        }
                    }
                }
            }
        }

        private static IDictionary<string, IPropertySymbol> GetAllPropertiesInTypeChain(INamedTypeSymbol attributeType)
        {
            var propertiesMap = new Dictionary<string, IPropertySymbol>(StringComparer.OrdinalIgnoreCase);
            foreach (INamedTypeSymbol currentType in attributeType.GetBaseTypesAndThis())
            {
                foreach (IPropertySymbol property in currentType.GetMembers().Where(m => m.Kind == SymbolKind.Property))
                {
                    if (!propertiesMap.ContainsKey(property.Name))
                    {
                        propertiesMap.Add(property.Name, property);
                    }
                }
            }

            return propertiesMap;
        }

        private static void AnalyzeParameters(Compilation compilation, IEnumerable<IParameterSymbol> parameters, IDictionary<string, IPropertySymbol> propertiesMap, INamedTypeSymbol attributeType, Action<Diagnostic> addDiagnostic)
        {
            foreach (IParameterSymbol parameter in parameters)
            {
                if (parameter.Type.Kind != SymbolKind.ErrorType)
                {
                    if (!propertiesMap.TryGetValue(parameter.Name, out IPropertySymbol property) ||
                        !parameter.Type.IsAssignableTo(property.Type, compilation))
                    {
                        // Add a public read-only property accessor for positional argument '{0}' of attribute '{1}'.
                        addDiagnostic(GetDefaultDiagnostic(parameter, attributeType));
                    }
                    else
                    {
                        if (property.GetMethod == null)
                        {
                            // Add a public read-only property accessor for positional argument '{0}' of attribute '{1}'.
                            addDiagnostic(GetDefaultDiagnostic(parameter, attributeType));
                        }
                        else if (property.DeclaredAccessibility != Accessibility.Public ||
                            property.GetMethod.DeclaredAccessibility != Accessibility.Public)
                        {
                            if (!property.ContainingType.Equals(attributeType))
                            {
                                // A non-public getter exists in one of the base types.
                                // However, we cannot be sure if the user can modify the base type (it could be from a third party library).
                                // So generate the default diagnostic instead of increase visibility here.

                                // Add a public read-only property accessor for positional argument '{0}' of attribute '{1}'.
                                addDiagnostic(GetDefaultDiagnostic(parameter, attributeType));
                            }
                            else
                            {
                                // If '{0}' is the property accessor for positional argument '{1}', make it public.
                                addDiagnostic(GetIncreaseVisibilityDiagnostic(parameter, property));
                            }
                        }

                        if (property.SetMethod != null &&
                            property.SetMethod.DeclaredAccessibility == Accessibility.Public &&
                            Equals(property.ContainingType, attributeType))
                        {
                            // Remove the property setter from '{0}' or reduce its accessibility because it corresponds to positional argument '{1}'.
                            addDiagnostic(GetRemoveSetterDiagnostic(parameter, property));
                        }
                    }
                }
            }
        }

        private static Diagnostic GetDefaultDiagnostic(IParameterSymbol parameter, INamedTypeSymbol attributeType)
        {
            // Add a public read-only property accessor for positional argument '{0}' of attribute '{1}'.
            return parameter.Locations.CreateDiagnostic(DefaultRule, new Dictionary<string, string?> { { "case", AddAccessorCase } }.ToImmutableDictionary(), parameter.Name, attributeType.Name);
        }

        private static Diagnostic GetIncreaseVisibilityDiagnostic(IParameterSymbol parameter, IPropertySymbol property)
        {
            // If '{0}' is the property accessor for positional argument '{1}', make it public.
            return property.GetMethod.Locations.CreateDiagnostic(IncreaseVisibilityRule, new Dictionary<string, string?> { { "case", MakePublicCase } }.ToImmutableDictionary(), property.Name, parameter.Name);
        }

        private static Diagnostic GetRemoveSetterDiagnostic(IParameterSymbol parameter, IPropertySymbol property)
        {
            // Remove the property setter from '{0}' or reduce its accessibility because it corresponds to positional argument '{1}'.
            return property.SetMethod.Locations.CreateDiagnostic(RemoveSetterRule, new Dictionary<string, string?> { { "case", RemoveSetterCase } }.ToImmutableDictionary(), property.Name, parameter.Name);
        }
    }
}
