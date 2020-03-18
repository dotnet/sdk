// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotExposeGenericLists : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1002";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotExposeGenericListsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotExposeGenericListsMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotExposeGenericListsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(context =>
            {
                var genericListType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericList1);

                context.RegisterSymbolAction(context =>
                {
                    var symbol = context.Symbol;
                    var methodSymbol = symbol as IMethodSymbol;

                    // FxCop compat: only analyze externally visible symbols by default.
                    if (!symbol.MatchesConfiguredVisibility(context.Options, Rule, context.CancellationToken))
                    {
                        return;
                    }

                    if (methodSymbol?.AssociatedSymbol != null)
                    {
                        return;
                    }

                    if (symbol.IsOverride ||
                        symbol.IsImplementationOfAnyInterfaceMember())
                    {
                        return;
                    }

                    // Handle symbol return type
                    var returnType = symbol.GetMemberType();
                    if (returnType != null && returnType.OriginalDefinition.Equals(genericListType))
                    {
                        context.ReportDiagnostic(symbol.CreateDiagnostic(Rule,
                            returnType.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                            symbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                    }

                    // Handle symbol parameters
                    var parameters = symbol.GetParameters();
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].Type.OriginalDefinition.Equals(genericListType) &&
                            (i != 0 || methodSymbol == null || !methodSymbol.IsExtensionMethod))
                        {
                            context.ReportDiagnostic(parameters[i].CreateDiagnostic(Rule,
                                parameters[i].ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                                symbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                        }
                    }
                }, SymbolKind.Field, SymbolKind.Property, SymbolKind.Method);
            });
        }
    }
}
