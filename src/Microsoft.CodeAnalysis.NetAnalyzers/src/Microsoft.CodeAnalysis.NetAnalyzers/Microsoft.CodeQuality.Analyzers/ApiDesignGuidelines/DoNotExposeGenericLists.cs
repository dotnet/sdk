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

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericList1, out var genericListType))
                {
                    return;
                }

                context.RegisterSymbolAction(context =>
                {
                    var field = (IFieldSymbol)context.Symbol;

                    // FxCop compat: only analyze externally visible symbols by default.
                    if (!context.Options.MatchesConfiguredVisibility(Rule, field, context.Compilation))
                    {
                        return;
                    }

                    if (field.Type != null && field.Type.OriginalDefinition.Equals(genericListType))
                    {
                        context.ReportDiagnostic(field.CreateDiagnostic(Rule,
                            field.Type.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                            field.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                    }
                }, SymbolKind.Field);

                context.RegisterSymbolAction(context =>
                {
                    var property = (IPropertySymbol)context.Symbol;

                    if (property.IsOverride ||
                        property.IsImplementationOfAnyInterfaceMember())
                    {
                        return;
                    }

                    // FxCop compat: only analyze externally visible symbols by default.
                    if (!context.Options.MatchesConfiguredVisibility(Rule, property, context.Compilation))
                    {
                        return;
                    }

                    if (property.Type != null && property.Type.OriginalDefinition.Equals(genericListType))
                    {
                        context.ReportDiagnostic(property.CreateDiagnostic(Rule,
                            property.Type.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                            property.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                    }
                }, SymbolKind.Property);

                context.RegisterSymbolAction(context =>
                {
                    var methodSymbol = (IMethodSymbol)context.Symbol;

                    // Bail-out for accessor
                    if (methodSymbol.AssociatedSymbol != null)
                    {
                        return;
                    }

                    if (methodSymbol.IsOverride ||
                        methodSymbol.IsImplementationOfAnyInterfaceMember())
                    {
                        return;
                    }

                    // FxCop compat: only analyze externally visible symbols by default.
                    if (!context.Options.MatchesConfiguredVisibility(Rule, methodSymbol, context.Compilation))
                    {
                        return;
                    }

                    // Handle symbol return type
                    if (methodSymbol.ReturnType != null && methodSymbol.ReturnType.OriginalDefinition.Equals(genericListType))
                    {
                        context.ReportDiagnostic(methodSymbol.CreateDiagnostic(Rule,
                            methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                            methodSymbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                    }

                    // Handle symbol parameters
                    for (int i = 0; i < methodSymbol.Parameters.Length; i++)
                    {
                        var parameter = methodSymbol.Parameters[i];

                        if (parameter.Type != null && parameter.Type.OriginalDefinition.Equals(genericListType) &&
                            (i != 0 || !methodSymbol.IsExtensionMethod))
                        {
                            context.ReportDiagnostic(parameter.CreateDiagnostic(Rule,
                                parameter.Type.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                                methodSymbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                        }
                    }
                }, SymbolKind.Method);
            });
        }
    }
}
