// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class MarkAssembliesWithAttributesDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        internal const string CA1016RuleId = "CA1016";
        internal const string CA1014RuleId = "CA1014";

        private static readonly LocalizableString s_localizableMessageCA1016 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MarkAssembliesWithAssemblyVersionTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescriptionCA1016 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MarkAssembliesWithAssemblyVersionDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor CA1016Rule = new DiagnosticDescriptor(CA1016RuleId,
                                                                         s_localizableMessageCA1016,
                                                                         s_localizableMessageCA1016,
                                                                         DiagnosticCategory.Design,
                                                                         DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                         isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                         description: s_localizableDescriptionCA1016,
                                                                         helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca1016-mark-assemblies-with-assemblyversionattribute",
                                                                         customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        private static readonly LocalizableString s_localizableMessageCA1014 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MarkAssembliesWithClsCompliantTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescriptionCA1014 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MarkAssembliesWithClsCompliantDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        internal static DiagnosticDescriptor CA1014Rule = new DiagnosticDescriptor(CA1014RuleId,
                                                                         s_localizableMessageCA1014,
                                                                         s_localizableMessageCA1014,
                                                                         DiagnosticCategory.Design,
                                                                         DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                         isEnabledByDefault: false,
                                                                         description: s_localizableDescriptionCA1014,
                                                                         helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca1014-mark-assemblies-with-clscompliantattribute",
                                                                         customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX ? ImmutableArray.Create(CA1016Rule, CA1014Rule) : ImmutableArray<DiagnosticDescriptor>.Empty;

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationAction(AnalyzeCompilation);
        }

        private static void AnalyzeCompilation(CompilationAnalysisContext context)
        {
            var assemblyVersionAttributeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReflectionAssemblyVersionAttribute);
            var assemblyComplianceAttributeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCLSCompliantAttribute);

            if (assemblyVersionAttributeSymbol == null && assemblyComplianceAttributeSymbol == null)
            {
                return;
            }

            bool assemblyVersionAttributeFound = false;
            bool assemblyComplianceAttributeFound = false;
            var razorCompiledItemAttribute = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreRazorHostingRazorCompiledItemAttribute);

            // Check all assembly level attributes for the target attribute
            foreach (AttributeData attribute in context.Compilation.Assembly.GetAttributes())
            {
                if (attribute.AttributeClass.Equals(assemblyVersionAttributeSymbol))
                {
                    // Mark the version attribute as found
                    assemblyVersionAttributeFound = true;
                }
                else if (attribute.AttributeClass.Equals(assemblyComplianceAttributeSymbol))
                {
                    // Mark the compliance attribute as found
                    assemblyComplianceAttributeFound = true;
                }
                else if (razorCompiledItemAttribute != null &&
                    attribute.AttributeClass.Equals(razorCompiledItemAttribute))
                {
                    // Bail out for dummy compilation for Razor code behind file.
                    return;
                }
            }

            if (!assemblyVersionAttributeFound && assemblyVersionAttributeSymbol != null)
            {
                context.ReportNoLocationDiagnostic(CA1016Rule);
            }

            if (!assemblyComplianceAttributeFound && assemblyComplianceAttributeSymbol != null)
            {
                context.ReportNoLocationDiagnostic(CA1014Rule);
            }
        }
    }
}
