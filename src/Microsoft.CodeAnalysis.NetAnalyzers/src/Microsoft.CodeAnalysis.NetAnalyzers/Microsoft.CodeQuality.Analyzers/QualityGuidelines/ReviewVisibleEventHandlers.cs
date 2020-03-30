// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA2109: Review visible event handlers
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ReviewVisibleEventHandlersAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2109";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ReviewVisibleEventHandlersTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageSecurity = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ReviewVisibleEventHandlersMessageSecurity), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageDefault = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ReviewVisibleEventHandlersMessageDefault), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ReviewVisibleEventHandlersDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor SecurityRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessageSecurity,
            DiagnosticCategory.Security,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static DiagnosticDescriptor DefaultRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessageDefault,
            DiagnosticCategory.Security,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(SecurityRule, DefaultRule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(context =>
            {
                var eventArgsType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEventArgs);
                var securityPermissionAttributeType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityPermissionsSecurityPermissionAttribute);

                context.RegisterSymbolAction(context =>
                {
                    var method = (IMethodSymbol)context.Symbol;

                    // FxCop compat: only analyze externally visible symbols by default.
                    if (!method.MatchesConfiguredVisibility(context.Options, DefaultRule, context.CancellationToken))
                    {
                        return;
                    }

                    if (method.IsOverride || method.IsImplementationOfAnyInterfaceMember())
                    {
                        return;
                    }

                    if (method.HasEventHandlerSignature(eventArgsType))
                    {
                        var rule = method.HasAttribute(securityPermissionAttributeType)
                            ? SecurityRule
                            : DefaultRule;

                        context.ReportDiagnostic(method.CreateDiagnostic(rule, method.Name));
                    }
                }, SymbolKind.Method);
            });
        }
    }
}