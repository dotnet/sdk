// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1018: <inheritdoc cref="MarkAttributesWithAttributeUsageTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class MarkAttributesWithAttributeUsageAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1018";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(MarkAttributesWithAttributeUsageTitle)),
            CreateLocalizableResourceString(nameof(MarkAttributesWithAttributeUsageMessageDefault)),
            DiagnosticCategory.Design,
            RuleLevel.IdeSuggestion,
            description: null,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                INamedTypeSymbol? attributeType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttribute);
                INamedTypeSymbol? attributeUsageAttributeType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttributeUsageAttribute);
                if (attributeType == null || attributeUsageAttributeType == null)
                {
                    return;
                }

                compilationContext.RegisterSymbolAction(context =>
                {
                    AnalyzeSymbol(
                        (INamedTypeSymbol)context.Symbol,
                        attributeType,
                        attributeUsageAttributeType,
                        static (context, diagnostic) => context.ReportDiagnostic(diagnostic),
                        context);
                },
                SymbolKind.NamedType);
            });
        }

        private static void AnalyzeSymbol<TContext>(INamedTypeSymbol symbol, INamedTypeSymbol attributeType, INamedTypeSymbol attributeUsageAttributeType, Action<TContext, Diagnostic> addDiagnostic, TContext context)
        {
            if (symbol.IsAbstract || symbol.BaseType == null || !symbol.BaseType.Equals(attributeType))
            {
                return;
            }

            if (!symbol.HasAnyAttribute(attributeUsageAttributeType))
            {
                addDiagnostic(context, symbol.CreateDiagnostic(Rule, symbol.Name));
            }
        }
    }
}
