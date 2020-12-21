// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectSuffixTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageDefault = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectSuffixMessageDefault), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageSpecialCollection = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectSuffixMessageSpecialCollection), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectSuffixDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor DefaultRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageDefault,
                                                                             DiagnosticCategory.Naming,
                                                                             RuleLevel.IdeHidden_BulkConfigurable,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor SpecialCollectionRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageSpecialCollection,
                                                                             DiagnosticCategory.Naming,
                                                                             RuleLevel.IdeHidden_BulkConfigurable,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DefaultRule, SpecialCollectionRule);

        // Tuple says <TypeInheritedOrImplemented, AppropriateSuffix, Bool value saying if the suffix can `Collection` or the `AppropriateSuffix`>s
        // The bool values are as mentioned in the Uri
        private static readonly List<(string typeName, string suffix, bool canSuffixBeCollection)> s_baseTypesAndTheirSuffix = new()
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
            ("System.Collections.Generic.IReadOnlyCollection`1", "Collection", false),
            ("System.Collections.Generic.IReadOnlyDictionary`2", "Dictionary", false),
            ("System.Collections.Generic.ISet`1", "Set", false),
            ("System.Data.DataSet", "DataSet", false),
            ("System.Data.DataTable", "DataTable", true),
            ("System.IO.Stream", "Stream", false),
            ("System.Security.IPermission","Permission", false),
            ("System.Security.Policy.IMembershipCondition", "Condition", false)
        };

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(AnalyzeCompilationStart);
        }

        private static void AnalyzeCompilationStart(CompilationStartAnalysisContext context)
        {
            var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);

            var baseTypeSuffixMapBuilder = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, SuffixInfo>();
            var interfaceTypeSuffixMapBuilder = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, SuffixInfo>();

            foreach (var (typeName, suffix, canSuffixBeCollection) in s_baseTypesAndTheirSuffix)
            {
                var wellKnownNamedType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(typeName);

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

            var baseTypeSuffixMap = baseTypeSuffixMapBuilder.ToImmutable();
            var interfaceTypeSuffixMap = interfaceTypeSuffixMapBuilder.ToImmutable();

            context.RegisterSymbolAction((saContext) =>
            {
                var namedTypeSymbol = (INamedTypeSymbol)saContext.Symbol;
                if (!saContext.Options.MatchesConfiguredVisibility(DefaultRule, namedTypeSymbol, saContext.Compilation))
                {
                    Debug.Assert(!saContext.Options.MatchesConfiguredVisibility(SpecialCollectionRule, namedTypeSymbol, saContext.Compilation));
                    return;
                }

                Debug.Assert(saContext.Options.MatchesConfiguredVisibility(SpecialCollectionRule, namedTypeSymbol, saContext.Compilation));

                var excludeIndirectBaseTypes = context.Options.GetBoolOptionValue(EditorConfigOptionNames.ExcludeIndirectBaseTypes, DefaultRule,
                   namedTypeSymbol, context.Compilation, defaultValue: true);

                var baseTypes = excludeIndirectBaseTypes
                    ? namedTypeSymbol.BaseType != null ? ImmutableArray.Create(namedTypeSymbol.BaseType) : ImmutableArray<INamedTypeSymbol>.Empty
                    : namedTypeSymbol.GetBaseTypes();

                var userTypeSuffixMap = context.Options.GetAdditionalRequiredSuffixesOption(DefaultRule, saContext.Symbol,
                    context.Compilation);

                if (TryGetTypeSuffix(baseTypes, baseTypeSuffixMap, userTypeSuffixMap, out var typeSuffixInfo))
                {
                    // SpecialCollectionRule - Rename 'LastInFirstOut<T>' to end in either 'Collection' or 'Stack'.
                    // DefaultRule - Rename 'MyStringObjectHashtable' to end in 'Dictionary'.
                    var rule = typeSuffixInfo.CanSuffixBeCollection ? SpecialCollectionRule : DefaultRule;
                    if ((typeSuffixInfo.CanSuffixBeCollection && !namedTypeSymbol.Name.EndsWith("Collection", StringComparison.Ordinal) && !namedTypeSymbol.Name.EndsWith(typeSuffixInfo.Suffix, StringComparison.Ordinal)) ||
                        (!typeSuffixInfo.CanSuffixBeCollection && !namedTypeSymbol.Name.EndsWith(typeSuffixInfo.Suffix, StringComparison.Ordinal)))
                    {
                        saContext.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(rule, namedTypeSymbol.ToDisplayString(), typeSuffixInfo.Suffix));
                    }

                    return;
                }

                var interfaces = excludeIndirectBaseTypes
                    ? namedTypeSymbol.Interfaces
                    : namedTypeSymbol.AllInterfaces;

                if (TryGetTypeSuffix(interfaces, interfaceTypeSuffixMap, userTypeSuffixMap, out var interfaceSuffixInfo) &&
                    !namedTypeSymbol.Name.EndsWith(interfaceSuffixInfo.Suffix, StringComparison.Ordinal))
                {
                    saContext.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(DefaultRule, namedTypeSymbol.ToDisplayString(), interfaceSuffixInfo.Suffix));
                }
            }
            , SymbolKind.NamedType);

            var eventArgsType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEventArgs);
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

        private static bool TryGetTypeSuffix(IEnumerable<INamedTypeSymbol> typeSymbols, ImmutableDictionary<INamedTypeSymbol, SuffixInfo> hardcodedMap,
            SymbolNamesWithValueOption<string?> userMap, [NotNullWhen(true)] out SuffixInfo? suffixInfo)
        {
            foreach (var type in typeSymbols)
            {
                // User specific mapping has higher priority than hardcoded one
                if (userMap.TryGetValue(type.OriginalDefinition, out var suffix))
                {
                    if (!RoslynString.IsNullOrWhiteSpace(suffix))
                    {
                        suffixInfo = SuffixInfo.Create(suffix, canSuffixBeCollection: false);
                        return true;
                    }
                }
                else if (hardcodedMap.TryGetValue(type.OriginalDefinition, out suffixInfo))
                {
                    return true;
                }
            }

            suffixInfo = null;
            return false;
        }
    }

    internal class SuffixInfo
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