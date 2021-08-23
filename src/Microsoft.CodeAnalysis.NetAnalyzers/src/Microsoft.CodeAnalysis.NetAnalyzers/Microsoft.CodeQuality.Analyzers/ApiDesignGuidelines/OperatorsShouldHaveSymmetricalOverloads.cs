// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA2226: Operators should have symmetrical overloads
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class OperatorsShouldHaveSymmetricalOverloadsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2226";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OperatorsShouldHaveSymmetricalOverloadsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(
            nameof(MicrosoftCodeQualityAnalyzersResources.Since_0_redefines_operator_1_it_should_also_redefine_operator_2),
            MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OperatorsShouldHaveSymmetricalOverloadsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.CandidateForRemoval,     // C# compiler reports an error
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(symbolAnalysisContext =>
            {
                var namedType = (INamedTypeSymbol)symbolAnalysisContext.Symbol;

                // FxCop compat: only analyze externally visible symbols by default.
                if (!symbolAnalysisContext.Options.MatchesConfiguredVisibility(Rule, namedType, symbolAnalysisContext.Compilation))
                {
                    return;
                }

                // NOTE(cyrusn): We use the C# syntax when reporting diagnostics for these issues.
                // That's what the old FxCop rule did so it doesn't seem like a big deal.
                CheckOperators(symbolAnalysisContext, namedType, WellKnownMemberNames.EqualityOperatorName, WellKnownMemberNames.InequalityOperatorName, "==", "!=");
                CheckOperators(symbolAnalysisContext, namedType, WellKnownMemberNames.GreaterThanOperatorName, WellKnownMemberNames.LessThanOperatorName, ">", "<");
                CheckOperators(symbolAnalysisContext, namedType, WellKnownMemberNames.GreaterThanOrEqualOperatorName, WellKnownMemberNames.LessThanOrEqualOperatorName, ">=", "<=");
            }, SymbolKind.NamedType);
        }

        private static void CheckOperators(
            SymbolAnalysisContext analysisContext, INamedTypeSymbol namedType,
            string memberName1, string memberName2,
            string opName1, string opName2)
        {
            var operators1 = namedType.GetMembers(memberName1);
            var operators2 = namedType.GetMembers(memberName2);
            CheckOperators(analysisContext, namedType, operators1, operators2, opName1, opName2);
            CheckOperators(analysisContext, namedType, operators2, operators1, opName2, opName1);
        }

        private static void CheckOperators(SymbolAnalysisContext analysisContext,
            INamedTypeSymbol namedType,
            ImmutableArray<ISymbol> operators1, ImmutableArray<ISymbol> operators2,
            string opName1, string opName2)
        {
            foreach (var operator1 in operators1)
            {
                // FxCop compat: only analyze externally visible symbols by default.
                if (!analysisContext.Options.MatchesConfiguredVisibility(Rule, operator1, analysisContext.Compilation))
                {
                    return;
                }

                if (!operator1.IsUserDefinedOperator())
                {
                    continue;
                }

                if (operator1.GetParameters().Length != 2)
                {
                    continue;
                }

                if (HasSymmetricOperator(operator1, operators2))
                {
                    continue;
                }

                // Operator was missing match.
                // Since_0_redefines_operator_1_it_should_also_redefine_operator_2
                analysisContext.ReportDiagnostic(operator1.CreateDiagnostic(
                    Rule, namedType.Name, opName1, opName2));
            }
        }

        private static bool HasSymmetricOperator(ISymbol operator1, ImmutableArray<ISymbol> operators2)
        {
            foreach (var operator2 in operators2)
            {
                if (!operator2.IsUserDefinedOperator())
                {
                    continue;
                }

                if (HasSameParameterTypes(operator1, operator2))
                {
                    // Operator has match.
                    return true;
                }
            }

            return false;
        }

        private static bool HasSameParameterTypes(ISymbol operator1, ISymbol operator2)
        {
            var parameters1 = operator1.GetParameters();
            var parameters2 = operator2.GetParameters();

            if (parameters1.Length != parameters2.Length)
            {
                return false;
            }

            for (var i = 0; i < parameters1.Length; i++)
            {
                if (!parameters1[i].Type.Equals(parameters2[i].Type))
                {
                    return false;
                }
            }

            return true;
        }
    }
}