// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA2231: A value type overrides Equals but does not implement the equality operator.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class OverloadOperatorEqualsOnOverridingValueTypeEqualsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2231";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OverloadOperatorEqualsOnOverridingValueTypeEqualsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OverloadOperatorEqualsOnOverridingValueTypeEqualsMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OverloadOperatorEqualsOnOverridingValueTypeEqualsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                         s_localizableTitle,
                                                                         s_localizableMessage,
                                                                         DiagnosticCategory.Usage,
                                                                         RuleLevel.IdeSuggestion,
                                                                         description: s_localizableDescription,
                                                                         isPortedFxCopRule: true,
                                                                         isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(context =>
            {
                var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
                if (namedTypeSymbol.IsValueType &&
                    !(namedTypeSymbol.IsRefLikeType && namedTypeSymbol.TypeKind == TypeKind.Struct) &&
                    context.Options.MatchesConfiguredVisibility(Rule, namedTypeSymbol, context.Compilation) &&
                    namedTypeSymbol.OverridesEquals() &&
                    !namedTypeSymbol.ImplementsEqualityOperators())
                {
                    context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(Rule));
                }
            },
            SymbolKind.NamedType);
        }
    }
}
