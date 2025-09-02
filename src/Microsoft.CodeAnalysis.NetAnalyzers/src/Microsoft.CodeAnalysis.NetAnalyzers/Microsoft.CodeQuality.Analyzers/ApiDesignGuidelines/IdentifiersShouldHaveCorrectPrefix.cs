// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1715: <inheritdoc cref="IdentifiersShouldHaveCorrectPrefixTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class IdentifiersShouldHaveCorrectPrefixAnalyzer : DiagnosticAnalyzer
    {
        public const string RuleId = "CA1715";

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(IdentifiersShouldHaveCorrectPrefixTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(IdentifiersShouldHaveCorrectPrefixDescription));

        public static readonly DiagnosticDescriptor InterfaceRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(IdentifiersShouldHaveCorrectPrefixMessageInterface)),
            DiagnosticCategory.Naming,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public static readonly DiagnosticDescriptor TypeParameterRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(IdentifiersShouldHaveCorrectPrefixMessageTypeParameter)),
            DiagnosticCategory.Naming,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(InterfaceRule, TypeParameterRule);

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
                        AnalyzeNamedTypeSymbol(
                            (INamedTypeSymbol)context.Symbol,
                            allowSingleLetterTypeParameters,
                            static (context, diagnostic) => context.ReportDiagnostic(diagnostic),
                            context);
                        break;

                    case SymbolKind.Method:
                        AnalyzeMethodSymbol(
                            (IMethodSymbol)context.Symbol,
                            allowSingleLetterTypeParameters,
                            static (context, diagnostic) => context.ReportDiagnostic(diagnostic),
                            context);
                        break;
                }
            },
                SymbolKind.Method,
                SymbolKind.NamedType);
        }

        private static void AnalyzeNamedTypeSymbol<TContext>(INamedTypeSymbol symbol, bool allowSingleLetterTypeParameters, Action<TContext, Diagnostic> addDiagnostic, TContext context)
        {
            AnalyzeTypeParameters(symbol.TypeParameters, allowSingleLetterTypeParameters, addDiagnostic, context);

            if (symbol.TypeKind == TypeKind.Interface &&
                symbol.IsPublic() &&
                !HasCorrectPrefix(symbol, 'I'))
            {
                addDiagnostic(context, symbol.CreateDiagnostic(InterfaceRule, symbol.Name));
            }
        }

        private static void AnalyzeMethodSymbol<TContext>(IMethodSymbol symbol, bool allowSingleLetterTypeParameters, Action<TContext, Diagnostic> addDiagnostic, TContext context)
            => AnalyzeTypeParameters(symbol.TypeParameters, allowSingleLetterTypeParameters, addDiagnostic, context);

        private static void AnalyzeTypeParameters<TContext>(ImmutableArray<ITypeParameterSymbol> typeParameters, bool allowSingleLetterTypeParameters, Action<TContext, Diagnostic> addDiagnostic, TContext context)
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

                    addDiagnostic(context, typeParameter.CreateDiagnostic(TypeParameterRule, typeParameter.Name));
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
