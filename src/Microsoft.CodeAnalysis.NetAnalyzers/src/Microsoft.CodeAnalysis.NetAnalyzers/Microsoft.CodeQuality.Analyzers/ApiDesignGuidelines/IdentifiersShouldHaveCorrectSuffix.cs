// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1710: Identifiers should have correct suffix
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class IdentifiersShouldHaveCorrectSuffixAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1710";
        internal const string Uri = "https://docs.microsoft.com/visualstudio/code-quality/ca1710-identifiers-should-have-correct-suffix";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectSuffixTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageDefault = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectSuffixMessageDefault), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageSpecialCollection = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectSuffixMessageSpecialCollection), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectSuffixDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor DefaultRule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageDefault,
                                                                             DiagnosticCategory.Naming,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: Uri,
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);
        internal static DiagnosticDescriptor SpecialCollectionRule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageSpecialCollection,
                                                                             DiagnosticCategory.Naming,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: Uri,
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DefaultRule, SpecialCollectionRule);

        // Tuple says <TypeInheritedOrImplemented, AppropriateSuffix, Bool value saying if the suffix can `Collection` or the `AppropriateSuffix`>s
        // The bool values are as mentioned in the Uri
        private static readonly List<(string typeName, string suffix, bool canSuffixBeCollection)> s_baseTypesAndTheirSuffix = new List<(string, string, bool)>()
                                                    {
                                                        ("System.Attribute", "Attribute", false),
                                                        ("System.EventArgs", "EventArgs", false),
                                                        ("System.Exception", "Exception", false),
                                                        ("System.Collections.ICollection", "Collection", false),
                                                        ("System.Collections.IDictionary", "Dictionary", false),
                                                        ("System.Collections.IEnumerable", "Collection", false),
                                                        ("System.Collections.Queue", "Queue", true),
                                                        ("System.Collections.Stack", "Stack", true),
                                                        ("System.Collections.Generic.Queue`1", "Queue", true),
                                                        ("System.Collections.Generic.Stack`1", "Stack", true),
                                                        ("System.Collections.Generic.ICollection`1", "Collection", false),
                                                        ("System.Collections.Generic.IDictionary`2", "Dictionary", false),
                                                        ("System.Collections.Generic.IReadOnlyDictionary`2", "Dictionary", false),
                                                        ("System.Data.DataSet", "DataSet", false),
                                                        ("System.Data.DataTable", "DataTable", true),
                                                        ("System.IO.Stream", "Stream", false),
                                                        ("System.Security.IPermission","Permission", false),
                                                        ("System.Security.Policy.IMembershipCondition", "Condition", false)
                                                    };

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(AnalyzeCompilationStart);
        }

        private static void AnalyzeCompilationStart(CompilationStartAnalysisContext context)
        {
            var baseTypeSuffixMapBuilder = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, SuffixInfo>();
            var interfaceTypeSuffixMapBuilder = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, SuffixInfo>();

            foreach (var (typeName, suffix, canSuffixBeCollection) in s_baseTypesAndTheirSuffix)
            {
                var wellKnownNamedType = context.Compilation.GetOrCreateTypeByMetadataName(typeName);

                if (wellKnownNamedType != null && wellKnownNamedType.OriginalDefinition != null)
                {
                    // If the type is interface
                    if (wellKnownNamedType.OriginalDefinition.TypeKind == TypeKind.Interface)
                    {
                        interfaceTypeSuffixMapBuilder.Add(wellKnownNamedType.OriginalDefinition, SuffixInfo.Create(suffix, canSuffixBeCollection));
                    }
                    else
                    {
                        baseTypeSuffixMapBuilder.Add(wellKnownNamedType.OriginalDefinition, SuffixInfo.Create(suffix, canSuffixBeCollection));
                    }
                }
            }

            var additionalTypesWithTheirSuffix = context.Options.GetAdditionalRequiredSuffixesOption(DefaultRule,
                context.Compilation, context.CancellationToken);
            foreach (var (type, suffix) in additionalTypesWithTheirSuffix)
            {
                if (type.OriginalDefinition.TypeKind == TypeKind.Interface)
                {
                    if (!interfaceTypeSuffixMapBuilder.ContainsKey(type.OriginalDefinition))
                    {
                        interfaceTypeSuffixMapBuilder.Add(type.OriginalDefinition, SuffixInfo.Create(suffix, false));
                    }
                }
                else
                {
                    if (!baseTypeSuffixMapBuilder.ContainsKey(type.OriginalDefinition))
                    {
                        baseTypeSuffixMapBuilder.Add(type.OriginalDefinition, SuffixInfo.Create(suffix, false));
                    }
                }
            }

            if (baseTypeSuffixMapBuilder.Count > 0 || interfaceTypeSuffixMapBuilder.Count > 0)
            {
                var baseTypeSuffixMap = baseTypeSuffixMapBuilder.ToImmutable();
                var interfaceTypeSuffixMap = interfaceTypeSuffixMapBuilder.ToImmutable();

                var excludeIndirectBaseTypes = context.Options.GetBoolOptionValue(EditorConfigOptionNames.ExcludeIndirectBaseTypes, DefaultRule,
                    defaultValue: false, cancellationToken: context.CancellationToken);

                context.RegisterSymbolAction((saContext) =>
                {
                    var namedTypeSymbol = (INamedTypeSymbol)saContext.Symbol;
                    if (!namedTypeSymbol.MatchesConfiguredVisibility(saContext.Options, DefaultRule, saContext.CancellationToken))
                    {
                        Debug.Assert(!namedTypeSymbol.MatchesConfiguredVisibility(saContext.Options, SpecialCollectionRule, saContext.CancellationToken));
                        return;
                    }

                    Debug.Assert(namedTypeSymbol.MatchesConfiguredVisibility(saContext.Options, SpecialCollectionRule, saContext.CancellationToken));

                    var baseTypes = excludeIndirectBaseTypes
                        ? (new[] { namedTypeSymbol.BaseType })
                        : namedTypeSymbol.GetBaseTypes();
                    var baseType = baseTypes.FirstOrDefault(bt => baseTypeSuffixMap.ContainsKey(bt.OriginalDefinition));
                    if (baseType != null)
                    {
                        var suffixInfo = baseTypeSuffixMap[baseType.OriginalDefinition];

                        // SpecialCollectionRule - Rename 'LastInFirstOut<T>' to end in either 'Collection' or 'Stack'.
                        // DefaultRule - Rename 'MyStringObjectHashtable' to end in 'Dictionary'.
                        var rule = suffixInfo.CanSuffixBeCollection ? SpecialCollectionRule : DefaultRule;
                        if ((suffixInfo.CanSuffixBeCollection && !namedTypeSymbol.Name.EndsWith("Collection", StringComparison.Ordinal) && !namedTypeSymbol.Name.EndsWith(suffixInfo.Suffix, StringComparison.Ordinal)) ||
                            (!suffixInfo.CanSuffixBeCollection && !namedTypeSymbol.Name.EndsWith(suffixInfo.Suffix, StringComparison.Ordinal)))
                        {

                            saContext.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(rule, namedTypeSymbol.ToDisplayString(), suffixInfo.Suffix));
                        }

                        return;
                    }

                    var interfaces = excludeIndirectBaseTypes
                        ? namedTypeSymbol.Interfaces
                        : namedTypeSymbol.AllInterfaces;
                    var implementedInterface = interfaces.FirstOrDefault(i => interfaceTypeSuffixMap.ContainsKey(i.OriginalDefinition));
                    if (implementedInterface != null)
                    {
                        var suffixInfo = interfaceTypeSuffixMap[implementedInterface.OriginalDefinition];
                        if (!namedTypeSymbol.Name.EndsWith(suffixInfo.Suffix, StringComparison.Ordinal))
                        {
                            saContext.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(DefaultRule, namedTypeSymbol.ToDisplayString(), suffixInfo.Suffix));
                        }
                    }
                }
                , SymbolKind.NamedType);

                var eventArgsType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEventArgs);
                if (eventArgsType != null)
                {
                    context.RegisterSymbolAction((saContext) =>
                    {
                        const string eventHandlerString = "EventHandler";
                        var eventSymbol = (IEventSymbol)saContext.Symbol;
                        if (!eventSymbol.Type.Name.EndsWith(eventHandlerString, StringComparison.Ordinal) &&
                            eventSymbol.Type.IsInSource() &&
                            eventSymbol.Type.TypeKind == TypeKind.Delegate &&
                            ((INamedTypeSymbol)eventSymbol.Type).DelegateInvokeMethod?.HasEventHandlerSignature(eventArgsType) == true)
                        {
                            saContext.ReportDiagnostic(eventSymbol.CreateDiagnostic(DefaultRule, eventSymbol.Type.Name, eventHandlerString));
                        }
                    },
                    SymbolKind.Event);
                }
            }
        }
    }

    class SuffixInfo
    {
        public string Suffix { get; private set; }
        public bool CanSuffixBeCollection { get; private set; }

        private SuffixInfo(
            string suffix,
            bool canSuffixBeCollection)
        {
            Suffix = suffix;
            CanSuffixBeCollection = canSuffixBeCollection;
        }

        internal static SuffixInfo Create(string suffix, bool canSuffixBeCollection)
        {
            return new SuffixInfo(suffix, canSuffixBeCollection);
        }
    }
}