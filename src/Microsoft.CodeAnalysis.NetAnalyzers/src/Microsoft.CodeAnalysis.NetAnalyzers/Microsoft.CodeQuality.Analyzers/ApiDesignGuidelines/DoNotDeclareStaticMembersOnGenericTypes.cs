// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1000: <inheritdoc cref="DoNotDeclareStaticMembersOnGenericTypesTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotDeclareStaticMembersOnGenericTypesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1000";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotDeclareStaticMembersOnGenericTypesTitle)),
            CreateLocalizableResourceString(nameof(DoNotDeclareStaticMembersOnGenericTypesMessage)),
            DiagnosticCategory.Design,
            RuleLevel.IdeHidden_BulkConfigurable, // Need to exclude members that use generic type parameter
            description: CreateLocalizableResourceString(nameof(DoNotDeclareStaticMembersOnGenericTypesDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

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

                    // Virtual members on generic types can't be called directly, so they don't suffer the problem this analyzer exists to prevent.
                    if (symbol.IsAbstract || symbol.IsVirtual)
                    {
                        return;
                    }

                    symbolAnalysisContext.ReportDiagnostic(symbol.CreateDiagnostic(Rule, symbol.Name));
                }, SymbolKind.Method, SymbolKind.Property);
        }
    }
}