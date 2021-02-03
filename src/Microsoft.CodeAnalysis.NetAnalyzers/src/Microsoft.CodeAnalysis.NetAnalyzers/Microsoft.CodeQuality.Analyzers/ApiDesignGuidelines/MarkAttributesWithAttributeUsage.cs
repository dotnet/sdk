// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1018: Custom attributes should have AttributeUsage attribute defined.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class MarkAttributesWithAttributeUsageAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1018";
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MarkAttributesWithAttributeUsageTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MarkAttributesWithAttributeUsageMessageDefault), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                    s_localizableTitle,
                                                                    s_localizableMessage,
                                                                    DiagnosticCategory.Design,
                                                                    RuleLevel.IdeSuggestion,
                                                                    description: null,
                                                                    isPortedFxCopRule: true,
                                                                    isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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
                    AnalyzeSymbol((INamedTypeSymbol)context.Symbol, attributeType, attributeUsageAttributeType, context.ReportDiagnostic);
                },
                SymbolKind.NamedType);
            });
        }

        private static void AnalyzeSymbol(INamedTypeSymbol symbol, INamedTypeSymbol attributeType, INamedTypeSymbol attributeUsageAttributeType, Action<Diagnostic> addDiagnostic)
        {
            if (symbol.IsAbstract || symbol.BaseType == null || !symbol.BaseType.Equals(attributeType))
            {
                return;
            }

            if (!symbol.HasAttribute(attributeUsageAttributeType))
            {
                addDiagnostic(symbol.CreateDiagnostic(Rule, symbol.Name));
            }
        }
    }
}
