// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using System.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1044: <inheritdoc cref="PropertiesShouldNotBeWriteOnlyTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PropertiesShouldNotBeWriteOnlyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1044";

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(PropertiesShouldNotBeWriteOnlyTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(PropertiesShouldNotBeWriteOnlyDescription));

        internal static readonly DiagnosticDescriptor AddGetterRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(PropertiesShouldNotBeWriteOnlyMessageAddGetter)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor MakeMoreAccessibleRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(PropertiesShouldNotBeWriteOnlyMessageMakeMoreAccessible)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(AddGetterRule, MakeMoreAccessibleRule);

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

            Debug.Assert(context.Options.MatchesConfiguredVisibility(MakeMoreAccessibleRule, property, context.Compilation) == context.Options.MatchesConfiguredVisibility(AddGetterRule, property, context.Compilation));

            // We handled the non-CA1044 cases earlier.  Now, we handle CA1044 cases
            // If there is no getter then it is not accessible
            if (property.IsWriteOnly)
            {
                // Only analyze externally visible properties by default
                if (context.Options.MatchesConfiguredVisibility(AddGetterRule, property, context.Compilation))
                {
                    context.ReportDiagnostic(property.CreateDiagnostic(AddGetterRule, property.Name));
                }
            }
            // Otherwise if there is a setter, check for its relative accessibility
            else if (!property.IsReadOnly && (property.GetMethod!.DeclaredAccessibility < property.SetMethod!.DeclaredAccessibility))
            {
                // Only analyze externally visible properties by default
                if (context.Options.MatchesConfiguredVisibility(MakeMoreAccessibleRule, property, context.Compilation))
                {
                    context.ReportDiagnostic(property.CreateDiagnostic(MakeMoreAccessibleRule, property.Name));
                }
            }
        }
    }
}
