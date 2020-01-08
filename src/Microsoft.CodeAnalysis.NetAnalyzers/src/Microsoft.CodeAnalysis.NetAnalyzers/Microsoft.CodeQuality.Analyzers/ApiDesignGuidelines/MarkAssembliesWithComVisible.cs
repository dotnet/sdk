// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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

        private static readonly LocalizableString s_localizableMessageA = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ChangeAssemblyLevelComVisibleToFalse), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageB = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AddAssemblyLevelComVisibleFalse), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static readonly DiagnosticDescriptor RuleA = new DiagnosticDescriptor(RuleId,
                                                                                       s_localizableTitle,
                                                                                       s_localizableMessageA,
                                                                                       DiagnosticCategory.Design,
                                                                                       DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                                       isEnabledByDefault: false,
                                                                                       description: s_localizableDescription,
                                                                                       helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca1017-mark-assemblies-with-comvisibleattribute",
                                                                                       customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        internal static readonly DiagnosticDescriptor RuleB = new DiagnosticDescriptor(RuleId,
                                                                                       s_localizableTitle,
                                                                                       s_localizableMessageB,
                                                                                       DiagnosticCategory.Design,
                                                                                       DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                                       isEnabledByDefault: false,
                                                                                       description: s_localizableDescription,
                                                                                       helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca1017-mark-assemblies-with-comvisibleattribute",
                                                                                       customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RuleA, RuleB);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationAction(AnalyzeCompilation);
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
                    if (attributeInstance.ConstructorArguments.Length > 0 &&
                        attributeInstance.ConstructorArguments[0].Kind == TypedConstantKind.Primitive &&
                        attributeInstance.ConstructorArguments[0].Value != null &
                        attributeInstance.ConstructorArguments[0].Value.Equals(true))
                    {
                        // Has the attribute, with the value 'true'.
                        context.ReportNoLocationDiagnostic(RuleA, context.Compilation.Assembly.Name);
                    }
                }
                else
                {
                    // No ComVisible attribute at all.
                    context.ReportNoLocationDiagnostic(RuleB, context.Compilation.Assembly.Name);
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
