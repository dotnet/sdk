// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using System.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class IdentifiersShouldHaveCorrectPrefixAnalyzer : DiagnosticAnalyzer
    {
        public const string RuleId = "CA1715";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectPrefixTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectPrefixDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageInterfaceRule = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectPrefixMessageInterface), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        public static readonly DiagnosticDescriptor InterfaceRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                    s_localizableTitle,
                                                                                    s_localizableMessageInterfaceRule,
                                                                                    DiagnosticCategory.Naming,
                                                                                    RuleLevel.IdeHidden_BulkConfigurable,
                                                                                    description: s_localizableDescription,
                                                                                    isPortedFxCopRule: true,
                                                                                    isDataflowRule: false);

        private static readonly LocalizableString s_localizableMessageTypeParameterRule = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectPrefixMessageTypeParameter), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        public static readonly DiagnosticDescriptor TypeParameterRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableMessageTypeParameterRule,
                                                                                      DiagnosticCategory.Naming,
                                                                                      RuleLevel.IdeHidden_BulkConfigurable,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: true,
                                                                                      isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(InterfaceRule, TypeParameterRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(
                (context) =>
            {
                // FxCop compat: only analyze externally visible symbols by default.
                if (!context.Options.MatchesConfiguredVisibility(InterfaceRule, context.Symbol, context.Compilation))
                {
                    Debug.Assert(!context.Options.MatchesConfiguredVisibility(TypeParameterRule, context.Symbol, context.Compilation));
                    return;
                }

                Debug.Assert(context.Options.MatchesConfiguredVisibility(TypeParameterRule, context.Symbol, context.Compilation));

                bool allowSingleLetterTypeParameters = context.Options.GetBoolOptionValue(
                    optionName: EditorConfigOptionNames.ExcludeSingleLetterTypeParameters,
                    rule: TypeParameterRule,
                    context.Symbol,
                    context.Compilation,
                    defaultValue: false);

                switch (context.Symbol.Kind)
                {
                    case SymbolKind.NamedType:
                        AnalyzeNamedTypeSymbol((INamedTypeSymbol)context.Symbol, allowSingleLetterTypeParameters, context.ReportDiagnostic);
                        break;

                    case SymbolKind.Method:
                        AnalyzeMethodSymbol((IMethodSymbol)context.Symbol, allowSingleLetterTypeParameters, context.ReportDiagnostic);
                        break;
                }
            },
                SymbolKind.Method,
                SymbolKind.NamedType);
        }

        private static void AnalyzeNamedTypeSymbol(INamedTypeSymbol symbol, bool allowSingleLetterTypeParameters, Action<Diagnostic> addDiagnostic)
        {
            AnalyzeTypeParameters(symbol.TypeParameters, allowSingleLetterTypeParameters, addDiagnostic);

            if (symbol.TypeKind == TypeKind.Interface &&
                symbol.IsPublic() &&
                !HasCorrectPrefix(symbol, 'I'))
            {
                addDiagnostic(symbol.CreateDiagnostic(InterfaceRule, symbol.Name));
            }
        }

        private static void AnalyzeMethodSymbol(IMethodSymbol symbol, bool allowSingleLetterTypeParameters, Action<Diagnostic> addDiagnostic)
            => AnalyzeTypeParameters(symbol.TypeParameters, allowSingleLetterTypeParameters, addDiagnostic);

        private static void AnalyzeTypeParameters(ImmutableArray<ITypeParameterSymbol> typeParameters, bool allowSingleLetterTypeParameters, Action<Diagnostic> addDiagnostic)
        {
            foreach (var typeParameter in typeParameters)
            {
                if (!HasCorrectPrefix(typeParameter, 'T'))
                {
                    // Check if single letter type parameters are allowed through configuration.
                    if (allowSingleLetterTypeParameters && typeParameter.Name.Length == 1)
                    {
                        continue;
                    }

                    addDiagnostic(typeParameter.CreateDiagnostic(TypeParameterRule, typeParameter.Name));
                }
            }
        }

        private static bool HasCorrectPrefix(ISymbol symbol, char prefix)
        {
            WordParser parser = new WordParser(symbol.Name, WordParserOptions.SplitCompoundWords, prefix);

            string? firstWord = parser.NextWord();

            if (firstWord == null || firstWord.Length > 1)
            {
                return false;
            }

            return firstWord[0] == prefix;
        }
    }
}
