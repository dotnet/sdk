// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1710: <inheritdoc cref="IdentifiersShouldHaveCorrectSuffixTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class IdentifiersShouldHaveCorrectSuffixAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1710";
        private const string CollectionSuffix = "Collection";
        private const string DictionarySuffix = "Dictionary";
        private const string SetSuffix = "Set";
        private const string QueueSuffix = "Queue";
        private const string StackSuffix = "Stack";
        private const string EventHandlerString = "EventHandler";

        private static readonly ImmutableArray<string> s_setCollectionSuffixes = ImmutableArray.Create(SetSuffix, CollectionSuffix);
        private static readonly ImmutableArray<string> s_queueCollectionSuffixes = ImmutableArray.Create(QueueSuffix, CollectionSuffix);
        private static readonly ImmutableArray<string> s_stackCollectionSuffixes = ImmutableArray.Create(StackSuffix, CollectionSuffix);
        private static readonly ImmutableArray<string> s_dictionaryCollectionSuffixes = ImmutableArray.Create(DictionarySuffix, CollectionSuffix);
        private static readonly ImmutableArray<string> s_collectionDictionarySetStackQueueSuffixes = ImmutableArray.Create(CollectionSuffix, DictionarySuffix, SetSuffix, StackSuffix, QueueSuffix);

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(IdentifiersShouldHaveCorrectSuffixTitle));
        private static readonly LocalizableString s_localizableMessageDefault = CreateLocalizableResourceString(nameof(IdentifiersShouldHaveCorrectSuffixMessageDefault));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(IdentifiersShouldHaveCorrectSuffixDescription));

        internal static readonly DiagnosticDescriptor OneSuffixRule = DiagnosticDescriptorHelper.Create(RuleId,
            s_localizableTitle,
            s_localizableMessageDefault,
            DiagnosticCategory.Naming,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);
        internal static readonly DiagnosticDescriptor MultipleSuffixesRule = DiagnosticDescriptorHelper.Create(RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(IdentifiersShouldHaveCorrectSuffixMessageMultiple)),
            DiagnosticCategory.Naming,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);
        internal static readonly DiagnosticDescriptor UserSuffixRule = DiagnosticDescriptorHelper.Create(RuleId,
            s_localizableTitle,
            s_localizableMessageDefault,
            DiagnosticCategory.Naming,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(OneSuffixRule, MultipleSuffixesRule, UserSuffixRule);

        // Define for each interface/type the collection of allowed suffixes (first item is the preferred suffix),
        // note that the order matters as the algorithm works on a first match basis.
        private static readonly ImmutableArray<(string typeName, ImmutableArray<string> suffixes)> s_baseTypesAndTheirSuffix =
            ImmutableArray.Create(
                // types
                (WellKnownTypeNames.SystemAttribute, ImmutableArray.Create("Attribute")),
                (WellKnownTypeNames.SystemEventArgs, ImmutableArray.Create("EventArgs")),
                (WellKnownTypeNames.SystemException, ImmutableArray.Create("Exception")),
                (WellKnownTypeNames.SystemCollectionsQueue, s_queueCollectionSuffixes),
                (WellKnownTypeNames.SystemCollectionsStack, s_stackCollectionSuffixes),
                (WellKnownTypeNames.SystemCollectionsGenericQueue1, s_queueCollectionSuffixes),
                (WellKnownTypeNames.SystemCollectionsGenericStack1, s_stackCollectionSuffixes),
                (WellKnownTypeNames.SystemDataDataSet, ImmutableArray.Create("DataSet")),
                (WellKnownTypeNames.SystemDataDataTable, ImmutableArray.Create("DataTable", CollectionSuffix)),
                (WellKnownTypeNames.SystemIOStream, ImmutableArray.Create("Stream")),
                // interfaces
                (WellKnownTypeNames.SystemCollectionsGenericIDictionary2, s_dictionaryCollectionSuffixes),
                (WellKnownTypeNames.SystemCollectionsGenericIReadOnlyDictionary2, s_dictionaryCollectionSuffixes),
                (WellKnownTypeNames.SystemCollectionsGenericISet1, s_setCollectionSuffixes),
                (WellKnownTypeNames.SystemCollectionsGenericIReadOnlySet1, s_setCollectionSuffixes),
                (WellKnownTypeNames.SystemCollectionsGenericICollection1, s_collectionDictionarySetStackQueueSuffixes),
                (WellKnownTypeNames.SystemCollectionsGenericIReadOnlyCollection1, s_collectionDictionarySetStackQueueSuffixes),
                (WellKnownTypeNames.SystemCollectionsIDictionary, s_dictionaryCollectionSuffixes),
                (WellKnownTypeNames.SystemCollectionsICollection, s_collectionDictionarySetStackQueueSuffixes),
                (WellKnownTypeNames.SystemCollectionsIEnumerable, s_collectionDictionarySetStackQueueSuffixes),
                (WellKnownTypeNames.SystemSecurityIPermission, ImmutableArray.Create("Permission")),
                (WellKnownTypeNames.SystemSecurityPolicyIMembershipCondition, ImmutableArray.Create("Condition"))
            );

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(AnalyzeCompilationStart);
        }

        private static void AnalyzeCompilationStart(CompilationStartAnalysisContext context)
        {
            var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);

            var baseTypeSuffixMapBuilder = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, ImmutableArray<string>>(SymbolEqualityComparer.Default);
            var interfaceTypeSuffixMapBuilder = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, ImmutableArray<string>>(SymbolEqualityComparer.Default);

            foreach (var (typeName, suffixes) in s_baseTypesAndTheirSuffix)
            {
                if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(typeName, out var wellKnownNamedType)
                    || wellKnownNamedType.OriginalDefinition == null)
                {
                    continue;
                }

                // If the type is interface
                if (wellKnownNamedType.OriginalDefinition.TypeKind == TypeKind.Interface)
                {
                    interfaceTypeSuffixMapBuilder.Add(wellKnownNamedType.OriginalDefinition, suffixes);
                }
                else
                {
                    baseTypeSuffixMapBuilder.Add(wellKnownNamedType.OriginalDefinition, suffixes);
                }
            }

            var baseTypeSuffixMap = baseTypeSuffixMapBuilder.ToImmutable();
            var interfaceTypeSuffixMap = interfaceTypeSuffixMapBuilder.ToImmutable();

            context.RegisterSymbolAction(context =>
            {
                var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
                if (!context.Options.MatchesConfiguredVisibility(OneSuffixRule, namedTypeSymbol, context.Compilation))
                {
                    Debug.Assert(!context.Options.MatchesConfiguredVisibility(MultipleSuffixesRule, namedTypeSymbol, context.Compilation));
                    Debug.Assert(!context.Options.MatchesConfiguredVisibility(UserSuffixRule, namedTypeSymbol, context.Compilation));
                    return;
                }

                Debug.Assert(context.Options.MatchesConfiguredVisibility(MultipleSuffixesRule, namedTypeSymbol, context.Compilation));
                Debug.Assert(context.Options.MatchesConfiguredVisibility(UserSuffixRule, namedTypeSymbol, context.Compilation));

                var excludeIndirectBaseTypes = context.Options.GetBoolOptionValue(EditorConfigOptionNames.ExcludeIndirectBaseTypes, OneSuffixRule,
                   namedTypeSymbol, context.Compilation, defaultValue: true);

                var baseTypes = excludeIndirectBaseTypes
                    ? namedTypeSymbol.BaseType != null ? ImmutableArray.Create(namedTypeSymbol.BaseType) : ImmutableArray<INamedTypeSymbol>.Empty
                    : namedTypeSymbol.GetBaseTypes();

                var userTypeSuffixMap = context.Options.GetAdditionalRequiredSuffixesOption(UserSuffixRule, context.Symbol, context.Compilation);

                if (TryGetTypeSuffix(baseTypes, baseTypeSuffixMap, userTypeSuffixMap, out var typesSuffixes, out var rule))
                {
                    if (!typesSuffixes.Any(suffix => namedTypeSymbol.Name.EndsWith(suffix, StringComparison.Ordinal)))
                    {
                        context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(rule, namedTypeSymbol.ToDisplayString(), typesSuffixes[0],
                            string.Join("', '", typesSuffixes.Skip(1))));
                    }
                }
                else
                {
                    var interfaces = excludeIndirectBaseTypes
                        ? namedTypeSymbol.Interfaces
                        : namedTypeSymbol.AllInterfaces;

                    if (TryGetTypeSuffix(interfaces, interfaceTypeSuffixMap, userTypeSuffixMap, out var interfaceSuffixes, out rule) &&
                        !interfaceSuffixes.Any(suffix => namedTypeSymbol.Name.EndsWith(suffix, StringComparison.Ordinal)))
                    {
                        context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(rule, namedTypeSymbol.ToDisplayString(), interfaceSuffixes[0],
                            string.Join("', '", interfaceSuffixes.Skip(1))));
                    }
                }
            }
            , SymbolKind.NamedType);

            var eventArgsType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEventArgs);
            if (eventArgsType == null)
            {
                return;
            }

            context.RegisterSymbolAction(context =>
            {
                var eventSymbol = (IEventSymbol)context.Symbol;
                if (!eventSymbol.Type.Name.EndsWith(EventHandlerString, StringComparison.Ordinal) &&
                    eventSymbol.Type.IsInSource() &&
                    eventSymbol.Type.TypeKind == TypeKind.Delegate &&
                    ((INamedTypeSymbol)eventSymbol.Type).DelegateInvokeMethod?.HasEventHandlerSignature(eventArgsType) == true)
                {
                    context.ReportDiagnostic(eventSymbol.CreateDiagnostic(OneSuffixRule, eventSymbol.Type.Name, EventHandlerString));
                }
            },
            SymbolKind.Event);
        }

        private static bool TryGetTypeSuffix(IEnumerable<INamedTypeSymbol> typeSymbols, ImmutableDictionary<INamedTypeSymbol, ImmutableArray<string>> hardcodedMap,
            SymbolNamesWithValueOption<string?> userMap, out ImmutableArray<string> suffixes, [NotNullWhen(true)] out DiagnosticDescriptor? rule)
        {
            foreach (var type in typeSymbols)
            {
                // User specific mapping has higher priority than hardcoded one
                if (userMap.TryGetValue(type.OriginalDefinition, out var suffix))
                {
                    if (!RoslynString.IsNullOrWhiteSpace(suffix))
                    {
                        suffixes = ImmutableArray.Create(suffix);
                        rule = UserSuffixRule;
                        return true;
                    }
                }
                else if (hardcodedMap.TryGetValue(type.OriginalDefinition, out suffixes))
                {
                    rule = suffixes.Length == 1 ? OneSuffixRule : MultipleSuffixesRule;
                    return true;
                }
            }

            suffixes = ImmutableArray<string>.Empty;
            rule = null;
            return false;
        }
    }
}
