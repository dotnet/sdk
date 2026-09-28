// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1055: <inheritdoc cref="UriReturnValuesShouldNotBeStringsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class UriReturnValuesShouldNotBeStringsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1055";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(UriReturnValuesShouldNotBeStringsTitle)),
            CreateLocalizableResourceString(nameof(UriReturnValuesShouldNotBeStringsMessage)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(UriReturnValuesShouldNotBeStringsDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            // this is stateless analyzer, can run concurrently
            context.EnableConcurrentExecution();

            // this has no meaning on running on generated code which user can't control
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(c =>
            {
                var uri = c.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemUri);
                if (uri == null)
                {
                    // we don't have required types
                    return;
                }

                var analyzer = new PerCompilationAnalyzer(uri);
                c.RegisterSymbolAction(analyzer.Analyze, SymbolKind.Method);
            });
        }

        private class PerCompilationAnalyzer
        {
            private readonly INamedTypeSymbol _uri;

            public PerCompilationAnalyzer(INamedTypeSymbol uri)
            {
                _uri = uri;
            }

            public void Analyze(SymbolAnalysisContext context)
            {
                var method = (IMethodSymbol)context.Symbol;

                // check basic stuff that FxCop checks.
                if (method.IsOverride || method.IsFromMscorlib(context.Compilation))
                {
                    // Methods defined within mscorlib are excluded from this rule,
                    // since mscorlib cannot depend on System.Uri, which is defined
                    // in System.dll
                    return;
                }

                if (!context.Options.MatchesConfiguredVisibility(Rule, method, context.Compilation))
                {
                    // only apply to methods that are exposed outside by default
                    return;
                }

                if (method.IsAccessorMethod() || method.ReturnType?.SpecialType != SpecialType.System_String)
                {
                    // return type must be string and it must be not an accessor method
                    return;
                }

                if (method.Parameters.ContainsParameterOfType(_uri))
                {
                    // If you take a Uri, and return a string, then it's ok
                    return;
                }

                if (!method.SymbolNameContainsUriWords(context.CancellationToken))
                {
                    // doesn't contain uri word in its name
                    return;
                }

                if (context.Options.IsConfiguredToSkipAnalysis(Rule, method, context.Compilation))
                {
                    // property is excluded from analysis
                    return;
                }

                context.ReportDiagnostic(method.CreateDiagnostic(Rule, method.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
            }
        }
    }
}