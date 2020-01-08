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
        private const string HelpLinkUri = "https://docs.microsoft.com/visualstudio/code-quality/ca3147-mark-verb-handlers-with-validateantiforgerytoken";

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

        internal static readonly DiagnosticDescriptor NoVerbsRule = new DiagnosticDescriptor(
            RuleId,
            Title,
            NoVerbsMessage,
            DiagnosticCategory.Security,
            DiagnosticHelpers.DefaultDiagnosticSeverity,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinkUri);

        internal static readonly DiagnosticDescriptor NoVerbsNoTokenRule = new DiagnosticDescriptor(
            RuleId,
            Title,
            NoVerbsNoTokenMessage,
            DiagnosticCategory.Security,
            DiagnosticHelpers.DefaultDiagnosticSeverity,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinkUri);

        internal static readonly DiagnosticDescriptor GetAndTokenRule = new DiagnosticDescriptor(
            RuleId,
            Title,
            GetAndTokenMessage,
            DiagnosticCategory.Security,
            DiagnosticHelpers.DefaultDiagnosticSeverity,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinkUri);

        internal static readonly DiagnosticDescriptor GetAndOtherAndTokenRule = new DiagnosticDescriptor(
            RuleId,
            Title,
            GetAndOtherAndTokenMessage,
            DiagnosticCategory.Security,
            DiagnosticHelpers.DefaultDiagnosticSeverity,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinkUri);

        internal static readonly DiagnosticDescriptor VerbsAndNoTokenRule = new DiagnosticDescriptor(
            RuleId,
            Title,
            VerbsAndNoTokenMessage,
            DiagnosticCategory.Security,
            DiagnosticHelpers.DefaultDiagnosticSeverity,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinkUri);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(NoVerbsRule, NoVerbsNoTokenRule, GetAndTokenRule, GetAndOtherAndTokenRule, VerbsAndNoTokenRule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(
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
                            if (!(symbolContext.Symbol is IMethodSymbol methodSymbol)
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
                                    symbolContext.ReportDiagnostic(Diagnostic.Create(NoVerbsRule, methodSymbol.Locations[0], methodSymbol.MetadataName));
                                }
                                else
                                {
                                    // no verbs, no antiforgery token attribute
                                    symbolContext.ReportDiagnostic(Diagnostic.Create(NoVerbsNoTokenRule, methodSymbol.Locations[0], methodSymbol.MetadataName));
                                }
                            }
                            else
                            {
                                // verbs are defined
                                if (isAntiforgeryTokenDefined)
                                {
                                    if (verbs.HasFlag(MvcHttpVerbs.Get))
                                    {
                                        symbolContext.ReportDiagnostic(Diagnostic.Create(GetAndTokenRule, methodSymbol.Locations[0], methodSymbol.MetadataName));

                                        if ((verbs & (MvcHttpVerbs.Post | MvcHttpVerbs.Put | MvcHttpVerbs.Delete | MvcHttpVerbs.Patch)) != MvcHttpVerbs.None)
                                        {
                                            // both verbs, antiforgery token attribute
                                            symbolContext.ReportDiagnostic(Diagnostic.Create(GetAndOtherAndTokenRule, methodSymbol.Locations[0], methodSymbol.MetadataName));
                                        }
                                    }
                                }
                                else
                                {
                                    if ((verbs & (MvcHttpVerbs.Post | MvcHttpVerbs.Put | MvcHttpVerbs.Delete | MvcHttpVerbs.Patch)) != MvcHttpVerbs.None)
                                    {
                                        // HttpPost, no antiforgery token attribute
                                        symbolContext.ReportDiagnostic(Diagnostic.Create(VerbsAndNoTokenRule, methodSymbol.Locations[0], methodSymbol.MetadataName));
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
