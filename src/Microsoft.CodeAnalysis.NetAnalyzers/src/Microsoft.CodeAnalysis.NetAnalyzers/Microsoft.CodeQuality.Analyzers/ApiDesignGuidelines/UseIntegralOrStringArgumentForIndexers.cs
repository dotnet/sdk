// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using System.Linq;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1043: Use Integral Or String Argument For Indexers
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseIntegralOrStringArgumentForIndexersAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1043";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UseIntegralOrStringArgumentForIndexersTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UseIntegralOrStringArgumentForIndexersMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UseIntegralOrStringArgumentForIndexersDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Design,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca1043-use-integral-or-string-argument-for-indexers",
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        private static readonly SpecialType[] s_allowedTypes = new[] {
                        SpecialType.System_String,
                        SpecialType.System_Int16,
                        SpecialType.System_Int32,
                        SpecialType.System_Int64,
                        SpecialType.System_Object,
                        SpecialType.System_UInt16,
                        SpecialType.System_UInt32,
                        SpecialType.System_UInt64
                        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Property);
        }

        private void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var symbol = (IPropertySymbol)context.Symbol;
            if (symbol.IsIndexer &&
                !symbol.IsOverride &&
                symbol.MatchesConfiguredVisibility(context.Options, Rule, context.CancellationToken))
            {
                if (symbol.GetParameters().Length == 1)
                {
                    ITypeSymbol paramType = symbol.GetParameters()[0].Type;

                    if (paramType.TypeKind == TypeKind.TypeParameter)
                    {
                        return;
                    }

                    if (paramType.TypeKind == TypeKind.Enum)
                    {
                        paramType = ((INamedTypeSymbol)paramType).EnumUnderlyingType;
                    }

                    if (!s_allowedTypes.Contains(paramType.SpecialType))
                    {
                        context.ReportDiagnostic(symbol.CreateDiagnostic(Rule));
                    }
                }
            }
        }
    }
}

