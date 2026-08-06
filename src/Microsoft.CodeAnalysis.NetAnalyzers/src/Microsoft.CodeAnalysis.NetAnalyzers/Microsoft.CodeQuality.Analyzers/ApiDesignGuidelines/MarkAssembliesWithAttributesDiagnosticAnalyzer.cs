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
    /// CA1016: <inheritdoc cref="MarkAssembliesWithAssemblyVersionTitle"/>
    /// CA1014: <inheritdoc cref="MarkAssembliesWithClsCompliantTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class MarkAssembliesWithAttributesDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        internal const string CA1016RuleId = "CA1016";
        internal const string CA1014RuleId = "CA1014";

        internal static readonly DiagnosticDescriptor CA1016Rule = DiagnosticDescriptorHelper.Create(
            CA1016RuleId,
            CreateLocalizableResourceString(nameof(MarkAssembliesWithAssemblyVersionTitle)),
            CreateLocalizableResourceString(nameof(MarkAssembliesWithAssemblyVersionMessage)),
            DiagnosticCategory.Design,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(MarkAssembliesWithAssemblyVersionDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isReportedAtCompilationEnd: true);

        internal static readonly DiagnosticDescriptor CA1014Rule = DiagnosticDescriptorHelper.Create(
            CA1014RuleId,
            CreateLocalizableResourceString(nameof(MarkAssembliesWithClsCompliantTitle)),
            CreateLocalizableResourceString(nameof(MarkAssembliesWithClsCompliantMessage)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,  // See https://github.com/dotnet/runtime/issues/44194
            description: CreateLocalizableResourceString(nameof(MarkAssembliesWithClsCompliantDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInAggressiveMode: false, // See https://github.com/dotnet/runtime/issues/44194
            isReportedAtCompilationEnd: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(CA1016Rule, CA1014Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationAction(AnalyzeCompilation);
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
                if (attribute.AttributeClass == null)
                {
                    continue;
                }

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
