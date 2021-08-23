// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1028: Enum Storage should be Int32
    /// Implementation slightly modified from original FxCop after discussing with Nick Guerrera
    /// FxCop implementation used 2 distinct diagnostic messages depending on the underlying type of the enum
    /// In this implementation, we have only 1 diagnostic message - "If possible, make the underlying type of '{0}'  System.Int32 instead of {1}."
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class EnumStorageShouldBeInt32Analyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1028";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.EnumStorageShouldBeInt32Title), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageNotInt32 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.EnumStorageShouldBeInt32Message), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.EnumStorageShouldBeInt32Description), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageNotInt32,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.CandidateForRemoval,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                INamedTypeSymbol? flagsAttribute = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemFlagsAttribute);
                if (flagsAttribute == null)
                {
                    return;
                }

                compilationContext.RegisterSymbolAction(symbolContext => AnalyzeSymbol(symbolContext, flagsAttribute), SymbolKind.NamedType);
            });
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol flagsAttribute)
        {
            var symbol = (INamedTypeSymbol)context.Symbol;

            if (symbol.TypeKind != TypeKind.Enum)
            {
                return;
            }

            SpecialType underlyingType = symbol.EnumUnderlyingType.SpecialType;
            if (underlyingType == SpecialType.System_Int32)
            {
                return;
            }

            // Check accessibility of enum matches configuration or is public if not configured
            if (!context.Options.MatchesConfiguredVisibility(Rule, symbol, context.Compilation))
            {
                return;
            }

            // If enum is Int64 and has Flags attributes then exit
            if (underlyingType == SpecialType.System_Int64 && symbol.HasAttribute(flagsAttribute))
            {
                return;
            }

            context.ReportDiagnostic(symbol.CreateDiagnostic(Rule, symbol.Name, symbol.EnumUnderlyingType));
        }
    }
}
