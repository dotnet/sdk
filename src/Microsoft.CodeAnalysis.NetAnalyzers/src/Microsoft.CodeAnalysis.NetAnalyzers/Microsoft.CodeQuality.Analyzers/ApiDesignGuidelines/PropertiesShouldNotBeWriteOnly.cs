// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using System.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1044: Properties should not be write only
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PropertiesShouldNotBeWriteOnlyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1044";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PropertiesShouldNotBeWriteOnlyTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageAddGetter = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PropertiesShouldNotBeWriteOnlyMessageAddGetter), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageMakeMoreAccessible = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PropertiesShouldNotBeWriteOnlyMessageMakeMoreAccessible), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PropertiesShouldNotBeWriteOnlyDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor AddGetterRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageAddGetter,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor MakeMoreAccessibleRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageMakeMoreAccessible,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(AddGetterRule, MakeMoreAccessibleRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Property);
        }

        /// <summary>
        /// Implementation for CA1044: Properties should not be write only
        /// </summary>
        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            if (context.Symbol is not IPropertySymbol property)
            {
                return;
            }

            // not raising a violation for when:
            //     property is overridden because the issue can only be fixed in the base type
            //     property is the implementation of any interface member
            if (property.IsOverride || property.IsImplementationOfAnyInterfaceMember())
            {
                return;
            }

            // Only analyze externally visible properties by default
            if (!context.Options.MatchesConfiguredVisibility(AddGetterRule, property, context.Compilation))
            {
                Debug.Assert(!context.Options.MatchesConfiguredVisibility(MakeMoreAccessibleRule, property, context.Compilation));
                return;
            }

            Debug.Assert(context.Options.MatchesConfiguredVisibility(MakeMoreAccessibleRule, property, context.Compilation));

            // We handled the non-CA1044 cases earlier.  Now, we handle CA1044 cases
            // If there is no getter then it is not accessible
            if (property.IsWriteOnly)
            {
                context.ReportDiagnostic(property.CreateDiagnostic(AddGetterRule, property.Name));
            }
            // Otherwise if there is a setter, check for its relative accessibility
            else if (!property.IsReadOnly && (property.GetMethod.DeclaredAccessibility < property.SetMethod.DeclaredAccessibility))
            {
                context.ReportDiagnostic(property.CreateDiagnostic(MakeMoreAccessibleRule, property.Name));
            }
        }
    }
}
