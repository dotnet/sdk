// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class MarkAssembliesWithComVisibleAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1017";
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MarkAssembliesWithComVisibleTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MarkAssembliesWithComVisibleDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageChangeComVisible = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ChangeAssemblyLevelComVisibleToFalse), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageAddComVisible = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AddAssemblyLevelComVisibleFalse), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static readonly DiagnosticDescriptor RuleChangeComVisible = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                       s_localizableTitle,
                                                                                       s_localizableMessageChangeComVisible,
                                                                                       DiagnosticCategory.Design,
                                                                                       RuleLevel.Disabled,
                                                                                       description: s_localizableDescription,
                                                                                       isPortedFxCopRule: true,
                                                                                       isDataflowRule: false,
                                                                                       isEnabledByDefaultInAggressiveMode: false,
                                                                                       isReportedAtCompilationEnd: true);

        internal static readonly DiagnosticDescriptor RuleAddComVisible = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                       s_localizableTitle,
                                                                                       s_localizableMessageAddComVisible,
                                                                                       DiagnosticCategory.Design,
                                                                                       RuleLevel.Disabled,
                                                                                       description: s_localizableDescription,
                                                                                       isPortedFxCopRule: true,
                                                                                       isDataflowRule: false,
                                                                                       isEnabledByDefaultInAggressiveMode: false,
                                                                                       isReportedAtCompilationEnd: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RuleChangeComVisible, RuleAddComVisible);

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

                AttributeData? attributeInstance = context.Compilation.Assembly.GetAttributes().FirstOrDefault(a => a.AttributeClass.Equals(comVisibleAttributeSymbol));

                if (attributeInstance != null)
                {
                    if (!attributeInstance.ConstructorArguments.IsEmpty &&
                        attributeInstance.ConstructorArguments[0].Kind == TypedConstantKind.Primitive &&
                        attributeInstance.ConstructorArguments[0].Value != null &
                        attributeInstance.ConstructorArguments[0].Value.Equals(true))
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
                    .Where(s => s.DeclaredAccessibility == Accessibility.Public)
                    .Any();
        }
    }
}
