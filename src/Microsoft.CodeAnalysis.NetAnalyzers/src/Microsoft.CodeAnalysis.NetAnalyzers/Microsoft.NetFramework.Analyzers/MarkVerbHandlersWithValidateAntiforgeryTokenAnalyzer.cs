// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetFramework.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public partial class MarkVerbHandlersWithValidateAntiforgeryTokenAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA3147";

        private static readonly LocalizableString Title = new LocalizableResourceString(
            nameof(MicrosoftNetFrameworkAnalyzersResources.MarkVerbHandlersWithValidateAntiforgeryTokenTitle),
            MicrosoftNetFrameworkAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetFrameworkAnalyzersResources));

        private static readonly LocalizableString NoVerbsMessage = new LocalizableResourceString(
            nameof(MicrosoftNetFrameworkAnalyzersResources.MarkVerbHandlersWithValidateAntiforgeryTokenNoVerbsMessage),
            MicrosoftNetFrameworkAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetFrameworkAnalyzersResources));

        private static readonly LocalizableString NoVerbsNoTokenMessage = new LocalizableResourceString(
            nameof(MicrosoftNetFrameworkAnalyzersResources.MarkVerbHandlersWithValidateAntiforgeryTokenNoVerbsNoTokenMessage),
            MicrosoftNetFrameworkAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetFrameworkAnalyzersResources));

        private static readonly LocalizableString GetAndTokenMessage = new LocalizableResourceString(
            nameof(MicrosoftNetFrameworkAnalyzersResources.MarkVerbHandlersWithValidateAntiforgeryTokenGetAndTokenMessage),
            MicrosoftNetFrameworkAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetFrameworkAnalyzersResources));

        private static readonly LocalizableString GetAndOtherAndTokenMessage = new LocalizableResourceString(
            nameof(MicrosoftNetFrameworkAnalyzersResources.MarkVerbHandlersWithValidateAntiforgeryTokenGetAndOtherAndTokenMessage),
            MicrosoftNetFrameworkAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetFrameworkAnalyzersResources));

        private static readonly LocalizableString VerbsAndNoTokenMessage = new LocalizableResourceString(
            nameof(MicrosoftNetFrameworkAnalyzersResources.MarkVerbHandlersWithValidateAntiforgeryTokenVerbsAndNoTokenMessage),
            MicrosoftNetFrameworkAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetFrameworkAnalyzersResources));

        internal static readonly DiagnosticDescriptor NoVerbsRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            Title,
            NoVerbsMessage,
            DiagnosticCategory.Security,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor NoVerbsNoTokenRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            Title,
            NoVerbsNoTokenMessage,
            DiagnosticCategory.Security,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor GetAndTokenRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            Title,
            GetAndTokenMessage,
            DiagnosticCategory.Security,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor GetAndOtherAndTokenRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            Title,
            GetAndOtherAndTokenMessage,
            DiagnosticCategory.Security,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor VerbsAndNoTokenRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            Title,
            VerbsAndNoTokenMessage,
            DiagnosticCategory.Security,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(NoVerbsRule, NoVerbsNoTokenRule, GetAndTokenRule, GetAndOtherAndTokenRule, VerbsAndNoTokenRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartContext) =>
                {
                    WellKnownTypeProvider wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartContext.Compilation);
                    INamedTypeSymbol? mvcControllerSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcController);
                    INamedTypeSymbol? mvcControllerBaseSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcControllerBase);
                    INamedTypeSymbol? actionResultSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcActionResult);

                    if ((mvcControllerSymbol == null && mvcControllerBaseSymbol == null) || actionResultSymbol == null)
                    {
                        // No MVC controllers that return an ActionResult here.
                        return;
                    }

                    MvcAttributeSymbols mvcAttributeSymbols = new MvcAttributeSymbols(compilationStartContext.Compilation);

                    compilationStartContext.RegisterSymbolAction(
                        (SymbolAnalysisContext symbolContext) =>
                        {
                            // TODO enhancements: Consider looking at IAsyncResult-based action methods.
                            if (symbolContext.Symbol is not IMethodSymbol methodSymbol
                                || methodSymbol.MethodKind != MethodKind.Ordinary
                                || methodSymbol.IsStatic
                                || !methodSymbol.IsPublic()
                                || !(methodSymbol.ReturnType.Inherits(actionResultSymbol)  // FxCop implementation only looked at ActionResult-derived return types.
                                     || wellKnownTypeProvider.IsTaskOfType(
                                            methodSymbol.ReturnType,
                                            (ITypeSymbol typeArgument) => typeArgument.Inherits(actionResultSymbol)))
                                || (!methodSymbol.ContainingType.Inherits(mvcControllerSymbol)
                                    && !methodSymbol.ContainingType.Inherits(mvcControllerBaseSymbol)))
                            {
                                return;
                            }

                            ImmutableArray<AttributeData> methodAttributes = methodSymbol.GetAttributes();
                            mvcAttributeSymbols.ComputeAttributeInfo(methodAttributes, out var verbs, out var isAntiforgeryTokenDefined, out var isAction);

                            if (!isAction)
                            {
                                return;
                            }

                            if (verbs == MvcHttpVerbs.None)
                            {
                                // no verbs specified
                                if (isAntiforgeryTokenDefined)
                                {
                                    // antiforgery token attribute is set, but verbs are not specified
                                    symbolContext.ReportDiagnostic(methodSymbol.CreateDiagnostic(NoVerbsRule, methodSymbol.MetadataName));
                                }
                                else
                                {
                                    // no verbs, no antiforgery token attribute
                                    symbolContext.ReportDiagnostic(methodSymbol.CreateDiagnostic(NoVerbsNoTokenRule, methodSymbol.MetadataName));
                                }
                            }
                            else
                            {
                                // verbs are defined
                                if (isAntiforgeryTokenDefined)
                                {
                                    if (verbs.HasFlag(MvcHttpVerbs.Get))
                                    {
                                        symbolContext.ReportDiagnostic(methodSymbol.CreateDiagnostic(GetAndTokenRule, methodSymbol.MetadataName));

                                        if ((verbs & (MvcHttpVerbs.Post | MvcHttpVerbs.Put | MvcHttpVerbs.Delete | MvcHttpVerbs.Patch)) != MvcHttpVerbs.None)
                                        {
                                            // both verbs, antiforgery token attribute
                                            symbolContext.ReportDiagnostic(methodSymbol.CreateDiagnostic(GetAndOtherAndTokenRule, methodSymbol.MetadataName));
                                        }
                                    }
                                }
                                else
                                {
                                    if ((verbs & (MvcHttpVerbs.Post | MvcHttpVerbs.Put | MvcHttpVerbs.Delete | MvcHttpVerbs.Patch)) != MvcHttpVerbs.None)
                                    {
                                        // HttpPost, no antiforgery token attribute
                                        symbolContext.ReportDiagnostic(methodSymbol.CreateDiagnostic(VerbsAndNoTokenRule, methodSymbol.MetadataName));
                                    }
                                }
                            }
                        },
                        SymbolKind.Method);
                }
            );
        }
    }
}
