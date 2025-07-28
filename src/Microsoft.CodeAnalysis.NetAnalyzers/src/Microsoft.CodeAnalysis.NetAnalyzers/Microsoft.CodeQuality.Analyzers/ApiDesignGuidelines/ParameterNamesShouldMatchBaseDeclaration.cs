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
    /// CA1725: <inheritdoc cref="ParameterNamesShouldMatchBaseDeclarationTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ParameterNamesShouldMatchBaseDeclarationAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1725";
        internal const string NewNamePropertyName = "NewName";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(ParameterNamesShouldMatchBaseDeclarationTitle)),
            CreateLocalizableResourceString(nameof(ParameterNamesShouldMatchBaseDeclarationMessage)),
            DiagnosticCategory.Naming,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: CreateLocalizableResourceString(nameof(ParameterNamesShouldMatchBaseDeclarationDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(AnalyzeMethodSymbol, SymbolKind.Method);
        }

        private static void AnalyzeMethodSymbol(SymbolAnalysisContext analysisContext)
        {
            var methodSymbol = (IMethodSymbol)analysisContext.Symbol;

            if (!analysisContext.Options.MatchesConfiguredVisibility(Rule, methodSymbol, analysisContext.Compilation) ||
                !(methodSymbol.CanBeReferencedByName || methodSymbol.IsImplementationOfAnyExplicitInterfaceMember())
                || !methodSymbol.Locations.Any(x => x.IsInSource)
                || string.IsNullOrWhiteSpace(methodSymbol.Name))
            {
                return;
            }

            if (!methodSymbol.IsOverride && !methodSymbol.IsImplementationOfAnyImplicitInterfaceMember())
            {
                return;
            }

            ImmutableArray<IMethodSymbol> originalDefinitions = methodSymbol.GetOriginalDefinitions();
            if (originalDefinitions.IsEmpty)
            {
                // We did not find any original definitions so we don't have to do anything.
                // This can happen when the method has an override modifier,
                // but does not have any valid method it is overriding.
                return;
            }

            IMethodSymbol? bestMatch = null;
            int bestMatchScore = -1;

            foreach (var originalDefinition in originalDefinitions)
            {
                // always prefer the method override, if it is available
                // (the overridden method will always be the first item in the list.)
                if (originalDefinition.ContainingType.TypeKind != TypeKind.Interface)
                {
                    bestMatch = originalDefinition;
                    break;
                }

                int currentMatchScore = 0;
                for (int i = 0; i < methodSymbol.Parameters.Length; i++)
                {
                    IParameterSymbol currentParameter = methodSymbol.Parameters[i];
                    IParameterSymbol originalParameter = originalDefinition.Parameters[i];

                    if (currentParameter.Name == originalParameter.Name)
                    {
                        currentMatchScore++;
                    }
                }

                if (currentMatchScore > bestMatchScore)
                {
                    bestMatch = originalDefinition;
                    bestMatchScore = currentMatchScore;

                    if (bestMatchScore == methodSymbol.Parameters.Length)
                    {
                        break;
                    }
                }
            }

            if (bestMatch == null)
            {
                return;
            }

            for (int i = 0; i < methodSymbol.Parameters.Length; i++)
            {
                IParameterSymbol currentParameter = methodSymbol.Parameters[i];
                IParameterSymbol bestMatchParameter = bestMatch.Parameters[i];
                if (bestMatchParameter.OriginalDefinition.Type is ITypeParameterSymbol)
                {
                    continue;
                }

                if (currentParameter.Name != bestMatchParameter.Name)
                {
                    var properties = ImmutableDictionary<string, string?>.Empty.SetItem(NewNamePropertyName, bestMatchParameter.Name);

                    analysisContext.ReportDiagnostic(currentParameter.CreateDiagnostic(Rule, properties, methodSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), currentParameter.Name, bestMatchParameter.Name, bestMatch.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                }
            }
        }
    }
}