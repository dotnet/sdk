// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1000: Do not declare static members on generic types
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotDeclareStaticMembersOnGenericTypesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1000";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotDeclareStaticMembersOnGenericTypesTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotDeclareStaticMembersOnGenericTypesMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotDeclareStaticMembersOnGenericTypesDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.IdeHidden_BulkConfigurable, // Need to exclude members that use generic type parameter
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(
                symbolAnalysisContext =>
                {
                    // Fxcop compat: fire only on public static members within externally visible generic types by default.
                    ISymbol symbol = symbolAnalysisContext.Symbol;
                    if (!symbol.IsStatic ||
                        symbol.DeclaredAccessibility != Accessibility.Public ||
                        !symbol.ContainingType.IsGenericType ||
                        !symbolAnalysisContext.Options.MatchesConfiguredVisibility(Rule, symbol.ContainingType, symbolAnalysisContext.Compilation))
                    {
                        return;
                    }

                    // Do not flag non-ordinary methods, such as conversions, operator overloads, etc.
                    if (symbol is IMethodSymbol methodSymbol &&
                        methodSymbol.MethodKind != MethodKind.Ordinary)
                    {
                        return;
                    }

                    symbolAnalysisContext.ReportDiagnostic(symbol.CreateDiagnostic(Rule, symbol.Name));
                }, SymbolKind.Method, SymbolKind.Property);
        }
    }
}