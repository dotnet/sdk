// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Microsoft.CodeQuality.Analyzers.Documentation
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1200: <inheritdoc cref="AvoidUsingCrefTagsWithAPrefixTitle"/>
    /// </summary>
    public abstract class AvoidUsingCrefTagsWithAPrefixAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1200";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AvoidUsingCrefTagsWithAPrefixTitle)),
            CreateLocalizableResourceString(nameof(AvoidUsingCrefTagsWithAPrefixMessage)),
            DiagnosticCategory.Documentation,
            RuleLevel.IdeHidden_BulkConfigurable,      // False positives would occur in multitargeting scenarios.
            description: CreateLocalizableResourceString(nameof(AvoidUsingCrefTagsWithAPrefixDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        protected static void ProcessAttribute(SyntaxNodeAnalysisContext context, SyntaxTokenList textTokens)
        {
            if (!textTokens.Any())
            {
                return;
            }

            var token = textTokens.First();

            if (token.Span.Length >= 2)
            {
                var text = token.Text;

                if (text[1] == ':')
                {
                    var location = Location.Create(token.SyntaxTree!, textTokens.Span);
                    context.ReportDiagnostic(Diagnostic.Create(Rule, location, text[..2]));
                }
            }
        }
    }
}