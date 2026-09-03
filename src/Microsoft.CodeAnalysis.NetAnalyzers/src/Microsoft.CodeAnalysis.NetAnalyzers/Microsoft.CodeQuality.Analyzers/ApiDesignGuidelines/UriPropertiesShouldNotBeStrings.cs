// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1056: <inheritdoc cref="UriPropertiesShouldNotBeStringsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class UriPropertiesShouldNotBeStringsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1056";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(UriPropertiesShouldNotBeStringsTitle)),
            CreateLocalizableResourceString(nameof(UriPropertiesShouldNotBeStringsMessage)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(UriPropertiesShouldNotBeStringsDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            // this is stateless analyzer, can run concurrently
            context.EnableConcurrentExecution();

            // this has no meaning on running on generated code which user can't control
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(c =>
            {
                var attribute = c.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttribute);
                if (attribute == null)
                {
                    // we don't have required types
                    return;
                }

                var analyzer = new PerCompilationAnalyzer(attribute);
                c.RegisterSymbolAction(analyzer.Analyze, SymbolKind.Property);
            });
        }

        private class PerCompilationAnalyzer
        {
            private readonly INamedTypeSymbol _attribute;

            public PerCompilationAnalyzer(INamedTypeSymbol attribute)
            {
                _attribute = attribute;
            }

            public void Analyze(SymbolAnalysisContext context)
            {
                var property = (IPropertySymbol)context.Symbol;

                // check basic stuff that FxCop checks.
                if (property.IsOverride || property.IsFromMscorlib(context.Compilation) || property.IsImplementationOfAnyInterfaceMember())
                {
                    // Methods defined within mscorlib are excluded from this rule,
                    // since mscorlib cannot depend on System.Uri, which is defined
                    // in System.dll
                    return;
                }

                if (!context.Options.MatchesConfiguredVisibility(Rule, property, context.Compilation))
                {
                    // only apply to methods that are exposed outside by default
                    return;
                }

                if (property.Type?.SpecialType != SpecialType.System_String)
                {
                    // not expected type
                    return;
                }

                if (property.ContainingType.DerivesFrom(_attribute, baseTypesOnly: true))
                {
                    // Attributes cannot accept System.Uri objects as positional or optional attributes
                    return;
                }

                if (!property.SymbolNameContainsUriWords(context.CancellationToken))
                {
                    // property name doesn't contain uri word
                    return;
                }

                if (context.Options.IsConfiguredToSkipAnalysis(Rule, property, context.Compilation))
                {
                    // property is excluded from analysis
                    return;
                }

                context.ReportDiagnostic(property.CreateDiagnostic(Rule, property.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
            }
        }
    }
}