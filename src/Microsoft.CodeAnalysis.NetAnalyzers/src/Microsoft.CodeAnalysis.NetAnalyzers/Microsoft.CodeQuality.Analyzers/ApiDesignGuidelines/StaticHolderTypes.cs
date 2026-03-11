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
    /// CA1052: <inheritdoc cref="StaticHolderTypesShouldBeStaticOrNotInheritable"/>
    /// </summary>
    /// <remarks>
    /// <para>
    /// This analyzer combines FxCop rules 1052 and 1053, with updated guidance. It detects
    /// "static holder types": types whose only members are static, except possibly for a
    /// default constructor. In C#, such a type should be marked static, and the default
    /// constructor removed. In VB, such a type should be replaced with a module.
    /// </para>
    /// <para>
    /// This analyzer behaves as similarly as possible to the existing implementations of the FxCop
    /// rules, even when those implementations appear to conflict with the MSDN documentation of
    /// those rules. Like
    /// FxCop, this analyzer does not emit a diagnostic when a non-default constructor is declared,
    /// even though the title of CA1053 is "Static holder types should not have constructors".
    /// Like FxCop, this analyzer does emit a diagnostic when the type has a private default
    /// constructor, even though the documentation of CA1053 says it should only trigger for public
    /// or protected default constructor. Like FxCop, this analyzer does not emit a diagnostic when
    /// class has a base class, however the diagnostic is emitted if class supports empty interface.
    /// </para>
    /// <para>
    /// The rationale for all of this is to facilitate a smooth transition from FxCop rules to the
    /// corresponding Roslyn-based analyzers.
    /// </para>
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class StaticHolderTypesAnalyzer : DiagnosticAnalyzer
    {
        public const string RuleId = "CA1052";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(StaticHolderTypesShouldBeStaticOrNotInheritable)),
            CreateLocalizableResourceString(nameof(StaticHolderTypeIsNotStatic)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: null,
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

            if (!symbol.IsStatic &&
                !symbol.IsAbstract &&
                !IsSealedAndVisualBasic(symbol) &&
                symbol.IsStaticHolderType() &&
                context.Options.MatchesConfiguredVisibility(Rule, symbol, context.Compilation))
            {
                context.ReportDiagnostic(symbol.CreateDiagnostic(Rule, symbol.Name));
            }

            static bool IsSealedAndVisualBasic(INamedTypeSymbol symbol)
                => symbol.IsSealed && symbol.Language == LanguageNames.VisualBasic;
        }
    }
}