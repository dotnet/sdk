// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using System;
using System.Diagnostics;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1030: Use events where appropriate
    /// <para>
    /// This rule detects methods that have names that ordinarily would be used for events.
    /// Events follow the Observer or Publish-Subscribe design pattern; they are used when a state change in one object must be communicated to other objects.
    /// If a method gets called in response to a clearly defined state change, the method should be invoked by an event handler.
    /// Objects that call the method should raise events instead of calling the method directly.
    /// </para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseEventsWhereAppropriateAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1030";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UseEventsWhereAppropriateTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UseEventsWhereAppropriateMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UseEventsWhereAppropriateDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.CandidateForRemoval,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(symbolContext =>
            {
                var method = (IMethodSymbol)symbolContext.Symbol;
                if (!IsEventLikeNameCandidate(method.Name))
                {
                    Debug.Assert(!HasEventLikeName(method), "fast check failed but eventual check succeeds?");
                    return;
                }

                // FxCop compat: bail out for implicitly declared methods, overridden methods, interface implementations,
                // constructors and finalizers and non-externally visible methods by default.
                if (method.IsImplicitlyDeclared ||
                    method.IsOverride ||
                    method.IsImplementationOfAnyInterfaceMember() ||
                    method.IsConstructor() ||
                    method.IsFinalizer() ||
                    !symbolContext.Options.MatchesConfiguredVisibility(Rule, method, symbolContext.Compilation))
                {
                    return;
                }

                if (HasEventLikeName(method))
                {
                    // Consider making '{0}' an event.
                    var diagnostic = method.CreateDiagnostic(Rule, method.Name);
                    symbolContext.ReportDiagnostic(diagnostic);
                }
            }, SymbolKind.Method);
        }

        private static bool IsEventLikeNameCandidate(string name)
        {
            return name.StartsWith("fire", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("raise", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("add", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("remove", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasEventLikeName(IMethodSymbol method)
        {
            WordParser parser = new WordParser(method.Name, WordParserOptions.SplitCompoundWords);

            string? word = parser.NextWord();

            // Check for 'FireXXX', 'RaiseXXX'
            if (string.Equals(word, "fire", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(word, "raise", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check for 'AddOnXXX', 'RemoveOnXXX'
            if (string.Equals(word, "add", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(word, "remove", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(parser.NextWord(), "on", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}