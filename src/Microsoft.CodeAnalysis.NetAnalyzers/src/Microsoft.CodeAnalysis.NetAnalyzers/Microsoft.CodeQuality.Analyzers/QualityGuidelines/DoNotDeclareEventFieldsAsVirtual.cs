// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1070: <inheritdoc cref="DoNotDeclareEventFieldsAsVirtualTitle"/>
    /// </summary>
#pragma warning disable RS1004 // Recommend adding language support to diagnostic analyzer - Construct is invalid in VB.NET
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1004 // Recommend adding language support to diagnostic analyzer
    public sealed class DoNotDeclareEventFieldsAsVirtual : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1070";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotDeclareEventFieldsAsVirtualTitle)),
            CreateLocalizableResourceString(nameof(DoNotDeclareEventFieldsAsVirtualMessage)),
            DiagnosticCategory.Design,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(DoNotDeclareEventFieldsAsVirtualDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(context =>
            {
                var eventSymbol = (IEventSymbol)context.Symbol;

                if (!eventSymbol.IsVirtual ||
                    eventSymbol.AddMethod?.IsImplicitlyDeclared == false ||
                    eventSymbol.RemoveMethod?.IsImplicitlyDeclared == false)
                {
                    return;
                }

                // FxCop compat: only analyze externally visible symbols by default.
                if (!context.Options.MatchesConfiguredVisibility(Rule, eventSymbol, context.Compilation))
                {
                    return;
                }

                context.ReportDiagnostic(eventSymbol.CreateDiagnostic(Rule, eventSymbol.Name));
            }, SymbolKind.Event);
        }
    }
}
