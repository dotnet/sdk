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
    /// CA1700: <inheritdoc cref="DoNotNameEnumValuesReservedTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotNameEnumValuesReserved : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1700";

        private static readonly ImmutableArray<string> reservedWords = ImmutableArray.Create("reserved");

        public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotNameEnumValuesReservedTitle)),
            CreateLocalizableResourceString(nameof(DoNotNameEnumValuesReservedMessage)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(DoNotNameEnumValuesReservedDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(context =>
            {
                var field = (IFieldSymbol)context.Symbol;

                if (field.ContainingType == null ||
                    field.ContainingType.TypeKind != TypeKind.Enum ||
                    !WordParser.ContainsWord(field.Name, WordParserOptions.SplitCompoundWords, reservedWords))
                {
                    return;
                }

                // FxCop compat: only analyze externally visible symbols by default.
                if (!context.Options.MatchesConfiguredVisibility(Rule, field, context.Compilation))
                {
                    return;
                }

                context.ReportDiagnostic(field.CreateDiagnostic(Rule, field.ContainingType.Name, field.Name));
            }, SymbolKind.Field);
        }
    }
}
