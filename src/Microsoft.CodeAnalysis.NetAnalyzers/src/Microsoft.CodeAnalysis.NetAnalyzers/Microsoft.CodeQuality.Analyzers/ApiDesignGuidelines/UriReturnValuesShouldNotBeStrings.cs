// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1055: Uri return values should not be strings
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class UriReturnValuesShouldNotBeStringsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1055";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UriReturnValuesShouldNotBeStringsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UriReturnValuesShouldNotBeStringsMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UriReturnValuesShouldNotBeStringsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
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
            // this is stateless analyzer, can run concurrently
            context.EnableConcurrentExecution();

            // this has no meaning on running on generated code which user can't control
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(c =>
            {
                var @string = c.Compilation.GetSpecialType(SpecialType.System_String);
                var uri = c.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemUri);
                if (@string == null || uri == null)
                {
                    // we don't have required types
                    return;
                }

                var analyzer = new PerCompilationAnalyzer(@string, uri);
                c.RegisterSymbolAction(analyzer.Analyze, SymbolKind.Method);
            });
        }

        private class PerCompilationAnalyzer
        {
            private readonly INamedTypeSymbol _string;
            private readonly INamedTypeSymbol _uri;

            public PerCompilationAnalyzer(INamedTypeSymbol @string, INamedTypeSymbol uri)
            {
                _string = @string;
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

                if (method.IsAccessorMethod() || method.ReturnType?.Equals(_string) != true)
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

                context.ReportDiagnostic(method.CreateDiagnostic(Rule, method.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
            }
        }
    }
}