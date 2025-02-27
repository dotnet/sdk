// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1874: <inheritdoc cref="UseRegexIsMatchMessage"/>
    /// CA1875: <inheritdoc cref="UseRegexCountMessage"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseRegexMembers : DiagnosticAnalyzer
    {
        internal const string RegexIsMatchRuleId = "CA1874";
        internal const string RegexCountRuleId = "CA1875";

        // Regex.Match(...).Success => Regex.IsMatch(...)
        internal static readonly DiagnosticDescriptor UseRegexIsMatchRuleId = DiagnosticDescriptorHelper.Create(RegexIsMatchRuleId,
            CreateLocalizableResourceString(nameof(UseRegexIsMatchTitle)),
            CreateLocalizableResourceString(nameof(UseRegexIsMatchMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(UseRegexIsMatchDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        // Regex.Matches(...).Count => Regex.Count(...)
        internal static readonly DiagnosticDescriptor UseRegexCountRuleId = DiagnosticDescriptorHelper.Create(RegexCountRuleId,
            CreateLocalizableResourceString(nameof(UseRegexCountTitle)),
            CreateLocalizableResourceString(nameof(UseRegexCountMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(UseRegexCountDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            UseRegexIsMatchRuleId,
            UseRegexCountRuleId);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(context =>
            {
                // Require that Regex and supporting types exist.
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextRegularExpressionsGroup, out var groupType) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextRegularExpressionsMatchCollection, out var matchCollectionType) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextRegularExpressionsRegex, out var regexType))
                {
                    return;
                }

                // Get the various members needed from Regex types.
                var groupSuccessSymbol = groupType.GetMembers("Success").FirstOrDefault();
                var matchCollectionCountSymbol = matchCollectionType.GetMembers("Count").FirstOrDefault();
                var regexMatchSymbols = regexType.GetMembers("Match");
                var regexMatchesSymbols = regexType.GetMembers("Matches");
                var regexIsMatchSymbols = regexType.GetMembers("IsMatch");
                var regexCountSymbols = regexType.GetMembers("Count");

                // Ensure we have the required member symbols (Regex.Count is optional).
                if (groupSuccessSymbol is null ||
                    matchCollectionCountSymbol is null ||
                    regexMatchSymbols.Length == 0 ||
                    regexMatchesSymbols.Length == 0 ||
                    regexIsMatchSymbols.Length == 0)
                {
                    return;
                }

                // Everything we're looking for is a property, so find all property references.
                context.RegisterOperationAction(context =>
                {
                    var initialPropRef = (IPropertyReferenceOperation)context.Operation;

                    // Regex.Match(...).Success. Look for Group.Success property access.
                    if (SymbolEqualityComparer.Default.Equals(initialPropRef.Property, groupSuccessSymbol))
                    {
                        if (initialPropRef.Instance is IInvocationOperation regexMatchInvocation &&
                            regexMatchSymbols.Contains(regexMatchInvocation.TargetMethod, SymbolEqualityComparer.Default) &&
                            HasMatchingOverload(regexMatchInvocation.TargetMethod, regexIsMatchSymbols))
                        {
                            context.ReportDiagnostic(initialPropRef.CreateDiagnostic(UseRegexIsMatchRuleId));
                        }
                        else
                        {
                            return;
                        }

                        return;
                    }

                    // Regex.Matches(...).Count. Look for MatchCollection.Count property access.
                    if (regexCountSymbols.Length != 0 &&
                        SymbolEqualityComparer.Default.Equals(initialPropRef.Property, matchCollectionCountSymbol))
                    {
                        if (initialPropRef.Instance is IInvocationOperation regexMatchesInvocation &&
                            regexMatchesSymbols.Contains(regexMatchesInvocation.TargetMethod, SymbolEqualityComparer.Default) &&
                            HasMatchingOverload(regexMatchesInvocation.TargetMethod, regexCountSymbols))
                        {
                            context.ReportDiagnostic(initialPropRef.CreateDiagnostic(UseRegexCountRuleId));
                        }

                        return;
                    }

                    // Look in overloads to see whether any of the methods there have exactly the same parameters
                    // by type as does the target method.
                    static bool HasMatchingOverload(ISymbol target, ImmutableArray<ISymbol> overloads)
                    {
                        ImmutableArray<IParameterSymbol> targetParameters = target.GetParameters();
                        foreach (ISymbol overload in overloads)
                        {
                            if (ParameterTypesMatch(targetParameters, overload.GetParameters()))
                            {
                                return true;
                            }
                        }

                        return false;
                    }

                    // Checks whether the two lists of parameters have the same types in the same order.
                    static bool ParameterTypesMatch(ImmutableArray<IParameterSymbol> left, ImmutableArray<IParameterSymbol> right)
                    {
                        if (left.Length != right.Length)
                        {
                            return false;
                        }

                        for (int i = 0; i < left.Length; i++)
                        {
                            if (!SymbolEqualityComparer.Default.Equals(left[i].Type, right[i].Type))
                            {
                                return false;
                            }
                        }

                        return true;
                    }
                }, OperationKind.PropertyReference);
            });
        }
    }
}