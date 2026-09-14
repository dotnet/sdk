// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1050: <inheritdoc cref="DeclareTypesInNamespacesTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DeclareTypesInNamespacesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1050";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DeclareTypesInNamespacesTitle)),
            CreateLocalizableResourceString(nameof(DeclareTypesInNamespacesMessage)),
            DiagnosticCategory.Design,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(DeclareTypesInNamespacesDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            var type = (INamedTypeSymbol)context.Symbol;

            if (type.DeclaredAccessibility == Accessibility.Public &&
                type.ContainingType == null &&
                type.ContainingNamespace.IsGlobalNamespace &&
                !type.IsTopLevelStatementsEntryPointType())
            {
                context.ReportDiagnostic(type.CreateDiagnostic(Rule));
            }
        }
    }
}
