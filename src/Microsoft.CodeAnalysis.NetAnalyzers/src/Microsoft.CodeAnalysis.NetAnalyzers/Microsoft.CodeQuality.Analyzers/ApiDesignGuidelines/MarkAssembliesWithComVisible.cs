// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1017: <inheritdoc cref="MarkAssembliesWithComVisibleTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class MarkAssembliesWithComVisibleAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1017";
        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(MarkAssembliesWithComVisibleTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(MarkAssembliesWithComVisibleDescription));

        internal static readonly DiagnosticDescriptor RuleChangeComVisible = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(ChangeAssemblyLevelComVisibleToFalse)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInAggressiveMode: false,
            isReportedAtCompilationEnd: true);

        internal static readonly DiagnosticDescriptor RuleAddComVisible = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(AddAssemblyLevelComVisibleFalse)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInAggressiveMode: false,
            isReportedAtCompilationEnd: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(RuleChangeComVisible, RuleAddComVisible);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationAction(AnalyzeCompilation);
        }

        private static void AnalyzeCompilation(CompilationAnalysisContext context)
        {
            if (AssemblyHasPublicTypes(context.Compilation.Assembly))
            {
                INamedTypeSymbol? comVisibleAttributeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesComVisibleAttribute);
                if (comVisibleAttributeSymbol == null)
                {
                    return;
                }

                AttributeData? attributeInstance = context.Compilation.Assembly.GetAttribute(comVisibleAttributeSymbol);

                if (attributeInstance != null)
                {
                    if (!attributeInstance.ConstructorArguments.IsEmpty &&
                        attributeInstance.ConstructorArguments[0].Kind == TypedConstantKind.Primitive &&
                        Equals(attributeInstance.ConstructorArguments[0].Value, true))
                    {
                        // Has the attribute, with the value 'true'.
                        context.ReportNoLocationDiagnostic(RuleChangeComVisible, context.Compilation.Assembly.Name);
                    }
                }
                else
                {
                    // No ComVisible attribute at all.
                    context.ReportNoLocationDiagnostic(RuleAddComVisible, context.Compilation.Assembly.Name);
                }
            }

            return;
        }

        private static bool AssemblyHasPublicTypes(IAssemblySymbol assembly)
        {
            return assembly
                    .GlobalNamespace
                    .GetMembers()
                    .OfType<INamedTypeSymbol>()
                    .Any(s => s.DeclaredAccessibility == Accessibility.Public);
        }
    }
}
