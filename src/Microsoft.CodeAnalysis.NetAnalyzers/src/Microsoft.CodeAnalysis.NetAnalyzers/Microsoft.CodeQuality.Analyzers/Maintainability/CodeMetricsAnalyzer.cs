// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeMetrics;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.CodeMetrics
{
    /// <summary>
    /// CA1501: Avoid excessive inheritance
    /// CA1502: Avoid excessive complexity
    /// CA1505: Avoid unmaintainable code
    /// CA1506: Avoid excessive class coupling
    /// CA1509: Invalid entry in code metrics rule specification file
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class CodeMetricsAnalyzer : DiagnosticAnalyzer
    {
        internal const string CA1501RuleId = "CA1501";
        internal const string CA1502RuleId = "CA1502";
        internal const string CA1505RuleId = "CA1505";
        internal const string CA1506RuleId = "CA1506";

        /// <summary>
        /// Configuration file to configure custom threshold values for supported code metrics.
        /// For example, the below entry changes the maximum allowed inheritance depth from the default value of 5 to 10:
        ///
        ///     # FORMAT:
        ///     # 'RuleId'(Optional 'SymbolKind'): 'Threshold'
        ///
        ///     CA1501: 10
        /// See CA1509 unit tests for more examples.
        /// </summary>
        private const string CodeMetricsConfigurationFile = "CodeMetricsConfig.txt";

        // New rule for invalid entries in CodeMetricsConfigurationFile.
        internal const string CA1509RuleId = "CA1509";

        private static readonly LocalizableString s_localizableTitleCA1501 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidExcessiveInheritanceTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageCA1501 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidExcessiveInheritanceMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescriptionCA1501 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidExcessiveInheritanceDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableTitleCA1502 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidExcessiveComplexityTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageCA1502 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidExcessiveComplexityMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescriptionCA1502 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidExcessiveComplexityDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableTitleCA1505 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidUnmantainableCodeTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageCA1505 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidUnmantainableCodeMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescriptionCA1505 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidUnmantainableCodeDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableTitleCA1506 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidExcessiveClassCouplingTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageCA1506 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidExcessiveClassCouplingMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescriptionCA1506 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidExcessiveClassCouplingDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableTitleCA1509 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.InvalidEntryInCodeMetricsConfigFileTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageCA1509 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.InvalidEntryInCodeMetricsConfigFileMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescriptionCA1509 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.InvalidEntryInCodeMetricsConfigFileDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor CA1501Rule = DiagnosticDescriptorHelper.Create(CA1501RuleId,
                                                                     s_localizableTitleCA1501,
                                                                     s_localizableMessageCA1501,
                                                                     DiagnosticCategory.Maintainability,
                                                                     RuleLevel.CandidateForRemoval,
                                                                     description: s_localizableDescriptionCA1501,
                                                                     isPortedFxCopRule: true,
                                                                     isDataflowRule: false,
                                                                     isEnabledByDefaultInAggressiveMode: false,
                                                                     isReportedAtCompilationEnd: true);

        internal static DiagnosticDescriptor CA1502Rule = DiagnosticDescriptorHelper.Create(CA1502RuleId,
                                                                     s_localizableTitleCA1502,
                                                                     s_localizableMessageCA1502,
                                                                     DiagnosticCategory.Maintainability,
                                                                     RuleLevel.CandidateForRemoval,
                                                                     description: s_localizableDescriptionCA1502,
                                                                     isPortedFxCopRule: true,
                                                                     isDataflowRule: false,
                                                                     isEnabledByDefaultInAggressiveMode: false,
                                                                     isReportedAtCompilationEnd: true);

        internal static DiagnosticDescriptor CA1505Rule = DiagnosticDescriptorHelper.Create(CA1505RuleId,
                                                                     s_localizableTitleCA1505,
                                                                     s_localizableMessageCA1505,
                                                                     DiagnosticCategory.Maintainability,
                                                                     RuleLevel.CandidateForRemoval,
                                                                     description: s_localizableDescriptionCA1505,
                                                                     isPortedFxCopRule: true,
                                                                     isDataflowRule: false,
                                                                     isEnabledByDefaultInAggressiveMode: false,
                                                                     isReportedAtCompilationEnd: true);

        internal static DiagnosticDescriptor CA1506Rule = DiagnosticDescriptorHelper.Create(CA1506RuleId,
                                                                     s_localizableTitleCA1506,
                                                                     s_localizableMessageCA1506,
                                                                     DiagnosticCategory.Maintainability,
                                                                     RuleLevel.CandidateForRemoval,
                                                                     description: s_localizableDescriptionCA1506,
                                                                     isPortedFxCopRule: true,
                                                                     isDataflowRule: false,
                                                                     isEnabledByDefaultInAggressiveMode: false,
                                                                     isReportedAtCompilationEnd: true);

        internal static DiagnosticDescriptor InvalidEntryInCodeMetricsConfigFileRule = DiagnosticDescriptorHelper.Create(CA1509RuleId,
                                                                     s_localizableTitleCA1509,
                                                                     s_localizableMessageCA1509,
                                                                     DiagnosticCategory.Maintainability,
                                                                     RuleLevel.CandidateForRemoval,
                                                                     description: s_localizableDescriptionCA1509,
                                                                     isPortedFxCopRule: false,
                                                                     isDataflowRule: false,
                                                                     isEnabledByDefaultInAggressiveMode: false,
                                                                     isReportedAtCompilationEnd: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(CA1501Rule, CA1502Rule, CA1505Rule, CA1506Rule, InvalidEntryInCodeMetricsConfigFileRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationAction(compilationContext =>
            {
                if (compilationContext.Compilation.SyntaxTrees.FirstOrDefault() is not SyntaxTree tree)
                {
                    return;
                }

                // Try read the additional file containing the code metrics configuration.
                if (!TryGetRuleIdToThresholdMap(
                        compilationContext.Options.AdditionalFiles,
                        compilationContext.CancellationToken,
                        out AdditionalText? additionalTextOpt,
                        out ImmutableDictionary<string, IReadOnlyList<(SymbolKind?, uint)>>? ruleIdToThresholdMap,
                        out List<Diagnostic>? invalidFileDiagnostics) &&
                    invalidFileDiagnostics != null)
                {
                    // Report any invalid additional file diagnostics.
                    foreach (var diagnostic in invalidFileDiagnostics)
                    {
                        compilationContext.ReportDiagnostic(diagnostic);
                    }

                    return;
                }

                var metricsAnalysisContext = new CodeMetricsAnalysisContext(compilationContext.Compilation, compilationContext.CancellationToken,
                    namedType => IsConfiguredToSkipFromInheritanceCount(namedType, compilationContext, tree));
                var computeTask = CodeAnalysisMetricData.ComputeAsync(metricsAnalysisContext);
                computeTask.Wait(compilationContext.CancellationToken);

                // Analyze code metrics tree and report diagnostics.
                analyzeMetricsData(computeTask.Result);

                void analyzeMetricsData(CodeAnalysisMetricData codeAnalysisMetricData)
                {
                    var symbol = codeAnalysisMetricData.Symbol;

                    // CA1501: Avoid excessive inheritance
                    if (symbol.Kind == SymbolKind.NamedType && codeAnalysisMetricData.DepthOfInheritance.HasValue)
                    {
                        uint? inheritanceThreshold = getThreshold(CA1501RuleId, symbol.Kind);
                        if (inheritanceThreshold.HasValue && codeAnalysisMetricData.DepthOfInheritance.Value > inheritanceThreshold.Value)
                        {
                            // '{0}' has an object hierarchy '{1}' levels deep within the defining module. If possible, eliminate base classes within the hierarchy to decrease its hierarchy level below '{2}': '{3}'
                            var arg1 = symbol.Name;
                            var arg2 = codeAnalysisMetricData.DepthOfInheritance;
                            var arg3 = inheritanceThreshold + 1;
                            var arg4 = string.Join(", ", ((INamedTypeSymbol)symbol).GetBaseTypes(t => !IsConfiguredToSkipFromInheritanceCount(t, compilationContext, tree)).Select(t => t.Name));
                            var diagnostic = symbol.CreateDiagnostic(CA1501Rule, arg1, arg2, arg3, arg4);
                            compilationContext.ReportDiagnostic(diagnostic);
                        }
                    }

                    // CA1502: Avoid excessive complexity
                    uint? complexityThreshold = getThreshold(CA1502RuleId, symbol.Kind);
                    if (complexityThreshold.HasValue && codeAnalysisMetricData.CyclomaticComplexity > complexityThreshold.Value)
                    {
                        // '{0}' has a cyclomatic complexity of '{1}'. Rewrite or refactor the code to decrease its complexity below '{2}'.
                        var arg1 = symbol.Name;
                        var arg2 = codeAnalysisMetricData.CyclomaticComplexity;
                        var arg3 = complexityThreshold.Value + 1;
                        var diagnostic = symbol.CreateDiagnostic(CA1502Rule, arg1, arg2, arg3);
                        compilationContext.ReportDiagnostic(diagnostic);
                    }

                    // CA1505: Avoid unmaintainable code
                    uint? maintainabilityIndexThreshold = getThreshold(CA1505RuleId, symbol.Kind);
                    if (maintainabilityIndexThreshold.HasValue && maintainabilityIndexThreshold.Value > codeAnalysisMetricData.MaintainabilityIndex)
                    {
                        // '{0}' has a maintainability index of '{1}'. Rewrite or refactor the code to increase its maintainability index (MI) above '{2}'.
                        var arg1 = symbol.Name;
                        var arg2 = codeAnalysisMetricData.MaintainabilityIndex;
                        var arg3 = maintainabilityIndexThreshold.Value - 1;
                        var diagnostic = symbol.CreateDiagnostic(CA1505Rule, arg1, arg2, arg3);
                        compilationContext.ReportDiagnostic(diagnostic);
                    }

                    // CA1506: Avoid excessive class coupling
                    uint? classCouplingThreshold = getThreshold(CA1506RuleId, symbol.Kind);
                    if (classCouplingThreshold.HasValue && codeAnalysisMetricData.CoupledNamedTypes.Count > classCouplingThreshold.Value)
                    {
                        // '{0}' is coupled with '{1}' different types from '{2}' different namespaces. Rewrite or refactor the code to decrease its class coupling below '{3}'.
                        var arg1 = symbol.Name;
                        var arg2 = codeAnalysisMetricData.CoupledNamedTypes.Count;
                        var arg3 = GetDistinctContainingNamespacesCount(codeAnalysisMetricData.CoupledNamedTypes);
                        var arg4 = classCouplingThreshold.Value + 1;
                        var diagnostic = symbol.CreateDiagnostic(CA1506Rule, arg1, arg2, arg3, arg4);
                        compilationContext.ReportDiagnostic(diagnostic);
                    }

                    foreach (var child in codeAnalysisMetricData.Children)
                    {
                        analyzeMetricsData(child);
                    }
                }

                uint? getThreshold(string ruleId, SymbolKind symbolKind)
                {
                    // Check if we have custom threshold value for the given ruleId and symbolKind.
                    if (ruleIdToThresholdMap != null &&
                        ruleIdToThresholdMap.TryGetValue(ruleId, out IReadOnlyList<(SymbolKind? symbolKindOpt, uint threshold)> values))
                    {
                        foreach ((SymbolKind? symbolKindOpt, uint threshold) in values)
                        {
                            if (symbolKindOpt.HasValue && symbolKindOpt.Value == symbolKind)
                            {
                                return threshold;
                            }
                        }

                        if (values.Count == 1 &&
                            values[0].symbolKindOpt == null &&
                            isApplicableByDefault(ruleId, symbolKind))
                        {
                            return values[0].threshold;
                        }
                    }

                    return getDefaultThreshold(ruleId, symbolKind);
                }

                static bool isApplicableByDefault(string ruleId, SymbolKind symbolKind)
                {
                    return ruleId switch
                    {
                        CA1501RuleId => symbolKind == SymbolKind.NamedType,
                        CA1502RuleId => symbolKind == SymbolKind.Method,
                        CA1505RuleId => symbolKind switch
                        {
                            SymbolKind.NamedType or SymbolKind.Method or SymbolKind.Field or SymbolKind.Property or SymbolKind.Event => true,
                            _ => false,
                        },
                        CA1506RuleId => symbolKind switch
                        {
                            SymbolKind.NamedType or SymbolKind.Method or SymbolKind.Field or SymbolKind.Property or SymbolKind.Event => true,
                            _ => false,
                        },
                        _ => throw new NotImplementedException(),
                    };
                }

                static uint? getDefaultThreshold(string ruleId, SymbolKind symbolKind)
                {
                    if (!isApplicableByDefault(ruleId, symbolKind))
                    {
                        return null;
                    }

                    // Compat: we match the default threshold values for old FxCop implementation.
                    return ruleId switch
                    {
                        CA1501RuleId => 5,

                        CA1502RuleId => 25,

                        CA1505RuleId => 10,

                        CA1506RuleId => symbolKind == SymbolKind.NamedType ? 95 : (uint)40,

                        _ => throw new NotImplementedException(),
                    };
                }
            });
        }

        private static bool TryGetRuleIdToThresholdMap(
            ImmutableArray<AdditionalText> additionalFiles,
            CancellationToken cancellationToken,
            [NotNullWhen(returnValue: true)] out AdditionalText? additionalText,
            [NotNullWhen(returnValue: true)] out ImmutableDictionary<string, IReadOnlyList<(SymbolKind?, uint)>>? ruleIdToThresholdMap,
            out List<Diagnostic>? invalidFileDiagnostics)
        {
            invalidFileDiagnostics = null;
            ruleIdToThresholdMap = null;

            // Parse the additional file for code metrics configuration.
            // Return false if there is no such additional file or it contains at least one invalid entry.
            additionalText = TryGetCodeMetricsConfigurationFile(additionalFiles, cancellationToken);
            return additionalText != null &&
                TryParseCodeMetricsConfigurationFile(additionalText, cancellationToken, out ruleIdToThresholdMap, out invalidFileDiagnostics);
        }

        private static AdditionalText? TryGetCodeMetricsConfigurationFile(ImmutableArray<AdditionalText> additionalFiles, CancellationToken cancellationToken)
        {
            StringComparer comparer = StringComparer.Ordinal;
            foreach (AdditionalText textFile in additionalFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string fileName = Path.GetFileName(textFile.Path);
                if (comparer.Equals(fileName, CodeMetricsConfigurationFile))
                {
                    return textFile;
                }
            }

            return null;
        }

        private static bool TryParseCodeMetricsConfigurationFile(
            AdditionalText additionalText,
            CancellationToken cancellationToken,
            [NotNullWhen(returnValue: true)] out ImmutableDictionary<string, IReadOnlyList<(SymbolKind?, uint)>>? ruleIdToThresholdMap,
            out List<Diagnostic>? invalidFileDiagnostics)
        {
            // Parse the additional file with Metric rule ID (which may contain an optional parenthesized SymbolKind suffix) and custom threshold.
            //     # FORMAT:
            //     # 'RuleId'(Optional 'SymbolKind'): 'Threshold'

            ruleIdToThresholdMap = null;
            invalidFileDiagnostics = null;

            var builder = ImmutableDictionary.CreateBuilder<string, IReadOnlyList<(SymbolKind?, uint)>>(StringComparer.OrdinalIgnoreCase);
            var lines = additionalText.GetText(cancellationToken).Lines;
            foreach (var line in lines)
            {
                var contents = line.ToString().Trim();
                if (contents.Length == 0 || contents.StartsWith("#", StringComparison.Ordinal))
                {
                    // Ignore empty lines and comments.
                    continue;
                }

                var parts = contents.Split(':');
                for (int i = 0; i < parts.Length; i++)
                {
                    parts[i] = parts[i].Trim();
                }

                var isInvalidLine = false;
                string key = parts[0];
                if (parts.Length != 2 ||                            // We require exactly one ':' separator in the line.
                    key.Any(char.IsWhiteSpace) ||                   // We do not allow white spaces in rule name.
                    !uint.TryParse(parts[1], out uint threshold))    // Value must be a non-negative integral threshold.
                {
                    isInvalidLine = true;
                }
                else
                {
                    SymbolKind? symbolKind = null;
                    string[] keyParts = key.Split('(');
                    switch (keyParts[0])
                    {
                        case CA1501RuleId:
                        case CA1502RuleId:
                        case CA1505RuleId:
                        case CA1506RuleId:
                            break;

                        default:
                            isInvalidLine = true;
                            break;
                    }

                    if (!isInvalidLine && keyParts.Length > 1)
                    {
                        if (keyParts.Length > 2 ||
                            keyParts[1].Length == 0 ||
                            keyParts[1].Last() != ')')
                        {
                            isInvalidLine = true;
                        }
                        else
                        {
                            // Remove the trailing ')'
                            var symbolKindStr = keyParts[1][0..^1];
                            switch (symbolKindStr)
                            {
                                case "Assembly":
                                    symbolKind = SymbolKind.Assembly;
                                    break;
                                case "Namespace":
                                    symbolKind = SymbolKind.Namespace;
                                    break;
                                case "Type":
                                    symbolKind = SymbolKind.NamedType;
                                    break;
                                case "Method":
                                    symbolKind = SymbolKind.Method;
                                    break;
                                case "Field":
                                    symbolKind = SymbolKind.Field;
                                    break;
                                case "Event":
                                    symbolKind = SymbolKind.Event;
                                    break;
                                case "Property":
                                    symbolKind = SymbolKind.Property;
                                    break;

                                default:
                                    isInvalidLine = true;
                                    break;
                            }
                        }
                    }

                    if (!isInvalidLine)
                    {
                        if (!builder.TryGetValue(keyParts[0], out var values))
                        {
                            values = new List<(SymbolKind?, uint)>();
                            builder.Add(keyParts[0], values);
                        }

                        ((List<(SymbolKind?, uint)>)values).Add((symbolKind, threshold));
                    }
                }

                if (isInvalidLine)
                {
                    // Invalid entry '{0}' in code metrics rule specification file '{1}'.
                    string arg1 = contents;
                    string arg2 = Path.GetFileName(additionalText.Path);
                    LinePositionSpan linePositionSpan = lines.GetLinePositionSpan(line.Span);
                    Location location = Location.Create(additionalText.Path, line.Span, linePositionSpan);
                    invalidFileDiagnostics ??= new List<Diagnostic>();
                    var diagnostic = Diagnostic.Create(InvalidEntryInCodeMetricsConfigFileRule, location, arg1, arg2);
                    invalidFileDiagnostics.Add(diagnostic);
                }
            }

            ruleIdToThresholdMap = builder.ToImmutable();
            return invalidFileDiagnostics == null;
        }

        private static int GetDistinctContainingNamespacesCount(IEnumerable<INamedTypeSymbol> namedTypes)
        {
            var distinctNamespaces = new HashSet<INamespaceSymbol>();
            foreach (var namedType in namedTypes)
            {
                if (namedType.ContainingNamespace != null)
                {
                    distinctNamespaces.Add(namedType.ContainingNamespace);
                }
            }

            return distinctNamespaces.Count;
        }

        private static bool IsConfiguredToSkipFromInheritanceCount(ISymbol symbol,
            CompilationAnalysisContext context, SyntaxTree tree)
        {
            // Compute code metrics.
            // For the calculation of the inheritance tree, we are allowing specific exclusions:
            //   - all types from System namespaces
            //   - all types/namespaces provided by the user
            // so that the calculation isn't unfair.
            // For example inheriting from WPF/WinForms UserControl makes your class over the default threshold,
            // yet there isn't anything you can do about it.
            var inheritanceExcludedTypes = context.Options.GetInheritanceExcludedSymbolNamesOption(CA1501Rule,
                tree, context.Compilation, defaultForcedValue: "N:System");

            if (inheritanceExcludedTypes.IsEmpty)
            {
                return false;
            }

            while (symbol != null)
            {
                if (inheritanceExcludedTypes.Contains(symbol))
                {
                    return true;
                }

                symbol = symbol.ContainingSymbol;
            }

            return false;
        }
    }
}