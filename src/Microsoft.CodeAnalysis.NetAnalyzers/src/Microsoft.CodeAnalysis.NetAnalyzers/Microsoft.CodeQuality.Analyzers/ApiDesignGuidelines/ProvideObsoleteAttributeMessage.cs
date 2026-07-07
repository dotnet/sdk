// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using System.Linq;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1041: <inheritdoc cref="ProvideObsoleteAttributeMessageTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ProvideObsoleteAttributeMessageAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1041";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(ProvideObsoleteAttributeMessageTitle)),
            CreateLocalizableResourceString(nameof(ProvideObsoleteAttributeMessageMessage)),
            DiagnosticCategory.Design,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(ProvideObsoleteAttributeMessageDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                INamedTypeSymbol? obsoleteAttributeType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemObsoleteAttribute);
                if (obsoleteAttributeType == null)
                {
                    return;
                }

                compilationContext.RegisterSymbolAction(sc => AnalyzeSymbol(sc, obsoleteAttributeType),
                    SymbolKind.NamedType,
                    SymbolKind.Method,
                    SymbolKind.Field,
                    SymbolKind.Property,
                    SymbolKind.Event);
            });
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol obsoleteAttributeType)
        {
            // FxCop compat: only analyze externally visible symbols by default
            if (!context.Options.MatchesConfiguredVisibility(Rule, context.Symbol, context.Compilation))
            {
                return;
            }

            foreach (AttributeData attribute in context.Symbol.GetAttributes(obsoleteAttributeType))
            {
                // ObsoleteAttribute has a constructor that takes no params and two
                // other constructors that take a message as the first param.
                // If there are no arguments specified or if the message argument is empty
                // then report a diagnostic.
                if (attribute.ApplicationSyntaxReference != null &&
                    (attribute.ConstructorArguments.IsEmpty ||
                    string.IsNullOrEmpty(attribute.ConstructorArguments.First().Value as string)))
                {
                    SyntaxNode node = attribute.ApplicationSyntaxReference.GetSyntax(context.CancellationToken);
                    context.ReportDiagnostic(node.CreateDiagnostic(Rule, context.Symbol.Name));
                }
            }
        }
    }
}