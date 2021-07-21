// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using System.Linq;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1725: Parameter names should match base declaration
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ParameterNamesShouldMatchBaseDeclarationAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1725";
        internal const string NewNamePropertyName = "NewName";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ParameterNamesShouldMatchBaseDeclarationTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ParameterNamesShouldMatchBaseDeclarationMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ParameterNamesShouldMatchBaseDeclarationDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Naming,
                                                                             RuleLevel.IdeHidden_BulkConfigurable,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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

            ImmutableArray<IMethodSymbol> originalDefinitions = GetOriginalDefinitions(methodSymbol);
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

                if (currentParameter.Name != bestMatchParameter.Name)
                {
                    var properties = ImmutableDictionary<string, string?>.Empty.SetItem(NewNamePropertyName, bestMatchParameter.Name);

                    analysisContext.ReportDiagnostic(currentParameter.CreateDiagnostic(Rule, properties, methodSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), currentParameter.Name, bestMatchParameter.Name, bestMatch.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                }
            }
        }

        private static ImmutableArray<IMethodSymbol> GetOriginalDefinitions(IMethodSymbol methodSymbol)
        {
            ImmutableArray<IMethodSymbol>.Builder originalDefinitionsBuilder = ImmutableArray.CreateBuilder<IMethodSymbol>();

            if (methodSymbol.IsOverride && (methodSymbol.OverriddenMethod != null))
            {
                originalDefinitionsBuilder.Add(methodSymbol.OverriddenMethod);
            }

            if (!methodSymbol.ExplicitInterfaceImplementations.IsEmpty)
            {
                originalDefinitionsBuilder.AddRange(methodSymbol.ExplicitInterfaceImplementations);
            }

            var typeSymbol = methodSymbol.ContainingType;
            var methodSymbolName = methodSymbol.Name;

            originalDefinitionsBuilder.AddRange(typeSymbol.AllInterfaces
                .SelectMany(m => m.GetMembers(methodSymbolName))
                .OfType<IMethodSymbol>()
                .Where(m => methodSymbol.Parameters.Length == m.Parameters.Length
                            && methodSymbol.Arity == m.Arity
                            && typeSymbol.FindImplementationForInterfaceMember(m) != null));

            return originalDefinitionsBuilder.ToImmutable();
        }
    }
}