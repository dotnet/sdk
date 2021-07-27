// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        private static readonly ImmutableHashSet<string> s_invalidPrefixes = ImmutableHashSet.Create(AfterKeyword, BeforeKeyword);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(context =>
            {
                var eventName = context.Symbol.Name;

                if (!eventName.StartsWith(BeforeKeyword, StringComparison.Ordinal) &&
                    !eventName.StartsWith(AfterKeyword, StringComparison.Ordinal))
                {
                    return;
                }

                var wordParse = new WordParser(eventName, WordParserOptions.SplitCompoundWords);

                if (wordParse.NextWord() is string firstWord &&
                    s_invalidPrefixes.Contains(firstWord) &&
                    wordParse.NextWord() is string) // Do not report if this is the only word
                {
                    context.ReportDiagnostic(context.Symbol.CreateDiagnostic(Rule));
                }
            }, SymbolKind.Event);
        }
    }
}
