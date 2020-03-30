// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class EventsShouldNotHaveBeforeOrAfterPrefix : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1713";
        private const string AfterKeyword = "After";
        private const string BeforeKeyword = "Before";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.EventsShouldNotHaveBeforeOrAfterPrefixTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.EventsShouldNotHaveBeforeOrAfterPrefixMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.EventsShouldNotHaveBeforeOrAfterPrefixDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Naming,
            RuleLevel.IdeSuggestion,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterSymbolAction(context =>
            {
                if (IsViolatingPrefix(context.Symbol.Name, AfterKeyword) ||
                    IsViolatingPrefix(context.Symbol.Name, BeforeKeyword))
                {
                    context.ReportDiagnostic(context.Symbol.CreateDiagnostic(Rule));
                }
            }, SymbolKind.Event);
        }

        private static bool IsViolatingPrefix(string eventName, string prefix)
            => eventName.StartsWith(prefix, StringComparison.Ordinal) &&
                eventName.Length > prefix.Length &&
                char.IsUpper(eventName[prefix.Length]);
    }
}
