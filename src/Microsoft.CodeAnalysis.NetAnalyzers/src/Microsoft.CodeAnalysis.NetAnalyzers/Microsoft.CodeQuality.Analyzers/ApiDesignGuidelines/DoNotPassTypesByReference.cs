// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1045: <inheritdoc cref="DoNotPassTypesByReferenceTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotPassTypesByReference : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1045";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotPassTypesByReferenceTitle)),
            CreateLocalizableResourceString(nameof(DoNotPassTypesByReferenceMessage)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(DoNotPassTypesByReferenceDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInAggressiveMode: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var outAttributeType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesOutAttribute);

                context.RegisterSymbolAction(context =>
                {
                    var methodSymbol = (IMethodSymbol)context.Symbol;

                    // FxCop compat: only analyze externally visible symbols by default.
                    if (!context.Options.MatchesConfiguredVisibility(Rule, methodSymbol, context.Compilation))
                    {
                        return;
                    }

                    if (methodSymbol.IsOverride ||
                        methodSymbol.IsImplementationOfAnyInterfaceMember())
                    {
                        return;
                    }

                    foreach (var parameterSymbol in methodSymbol.Parameters)
                    {
                        // VB.NET out is a ref parameter with the OutAttribute
                        if (parameterSymbol.RefKind == RefKind.Ref &&
                            !parameterSymbol.HasAnyAttribute(outAttributeType))
                        {
                            context.ReportDiagnostic(parameterSymbol.CreateDiagnostic(Rule, parameterSymbol.Name));
                        }
                    }
                }, SymbolKind.Method);
            });
        }
    }
}
