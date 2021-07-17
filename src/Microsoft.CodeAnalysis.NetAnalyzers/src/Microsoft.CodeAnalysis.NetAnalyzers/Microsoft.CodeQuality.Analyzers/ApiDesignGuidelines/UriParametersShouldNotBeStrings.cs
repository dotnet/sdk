// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1054: Uri parameters should not be strings
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class UriParametersShouldNotBeStringsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1054";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UriParametersShouldNotBeStringsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UriParametersShouldNotBeStringsMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UriParametersShouldNotBeStringsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

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
                var uri = c.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemUri);
                var attribute = c.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttribute);
                if (uri == null || attribute == null)
                {
                    // we don't have required types
                    return;
                }

                var analyzer = new PerCompilationAnalyzer(uri, attribute);
                c.RegisterSymbolAction(analyzer.Analyze, SymbolKind.Method);
            });
        }

        private class PerCompilationAnalyzer
        {
            private readonly INamedTypeSymbol _uri;
            private readonly INamedTypeSymbol _attribute;

            public PerCompilationAnalyzer(INamedTypeSymbol uri, INamedTypeSymbol attribute)
            {
                _uri = uri;
                _attribute = attribute;
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

                var stringParameters = method.Parameters.GetParametersOfType(SpecialType.System_String);
                if (!stringParameters.Any())
                {
                    // no string parameter. not interested.
                    return;
                }

                // now do cheap string check whether those string parameter contains uri word list we are looking for.
                if (!stringParameters.ParameterNamesContainUriWordSubstring(context.CancellationToken))
                {
                    // no string parameter that contains what we are looking for.
                    return;
                }

                if (method.ContainingType.DerivesFrom(_attribute, baseTypesOnly: true))
                {
                    // Attributes cannot accept System.Uri objects as positional or optional attributes
                    return;
                }

                // now we do more expensive word parsing to find exact parameter that contains url in parameter name
                var indices = method.GetParameterIndices(stringParameters.GetParametersThatContainUriWords(context.CancellationToken), context.CancellationToken);

                var overloads = method.ContainingType.GetMembers(method.Name).OfType<IMethodSymbol>();
                foreach (var index in indices)
                {
                    var overload = method.GetMatchingOverload(overloads, index, _uri, context.CancellationToken);
                    if (overload == null)
                    {
                        var parameter = method.Parameters[index];
                        context.ReportDiagnostic(parameter.CreateDiagnostic(Rule, parameter.Name, method.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                    }
                }
            }
        }
    }
}