// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1012: <inheritdoc cref="AbstractTypesShouldNotHaveConstructorsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AbstractTypesShouldNotHaveConstructorsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1012";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AbstractTypesShouldNotHaveConstructorsTitle)),
            CreateLocalizableResourceString(nameof(AbstractTypesShouldNotHaveConstructorsMessage)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(AbstractTypesShouldNotHaveConstructorsDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var symbol = (INamedTypeSymbol)context.Symbol;
            if (symbol.IsAbstract &&
                context.Options.MatchesConfiguredVisibility(Rule, symbol, context.Compilation))
            {
                bool hasAnyPublicConstructors =
                    symbol.InstanceConstructors.Any(
                        constructor => constructor.DeclaredAccessibility == Accessibility.Public);

                if (hasAnyPublicConstructors)
                {
                    context.ReportDiagnostic(symbol.CreateDiagnostic(Rule, symbol.Name));
                }
            }
        }
    }
}
