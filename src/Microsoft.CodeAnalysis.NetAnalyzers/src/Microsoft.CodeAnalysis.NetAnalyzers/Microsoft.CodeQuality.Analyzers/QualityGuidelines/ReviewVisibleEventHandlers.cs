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
        private static readonly LocalizableString s_localizableMessageDefault = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ReviewVisibleEventHandlersMessageDefault), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ReviewVisibleEventHandlersDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessageDefault,
            DiagnosticCategory.Security,
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
                var eventArgsType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEventArgs);

                context.RegisterSymbolAction(context =>
                {
                    var method = (IMethodSymbol)context.Symbol;

                    if (!method.IsExternallyVisible())
                    {
                        return;
                    }

                    if (!method.HasEventHandlerSignature(eventArgsType))
                    {
                        return;
                    }

                    if (method.IsOverride || method.IsImplementationOfAnyInterfaceMember())
                    {
                        return;
                    }

                    context.ReportDiagnostic(method.CreateDiagnostic(Rule, method.Name));
                }, SymbolKind.Method);
            });
        }
    }
}