// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
    /// <summary>
    /// CA1710: Identifiers should have correct suffix
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

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectSuffixTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageDefault = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectSuffixMessageDefault), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageSpecialCollection = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectSuffixMessageMultiple), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldHaveCorrectSuffixDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor OneSuffixRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageDefault,
                                                                             DiagnosticCategory.Naming,
                                                                             RuleLevel.IdeHidden_BulkConfigurable,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor MultipleSuffixesRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageSpecialCollection,
                                                                             DiagnosticCategory.Naming,
                                                                             RuleLevel.IdeHidden_BulkConfigurable,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor UserSuffixRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageDefault,
                                                                             DiagnosticCategory.Naming,
                                                                             RuleLevel.IdeHidden_BulkConfigurable,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(OneSuffixRule, MultipleSuffixesRule);

        // Define for each interface/type the collection of allowed suffixes (first item is the preferred suffix),
        // note that the order matters as the algorithm works on a first match basis.
        private static readonly ImmutableArray<(string typeName, ImmutableArray<string> suffixes)> s_baseTypesAndTheirSuffix =
            ImmutableArray.Create(
                // types
                (WellKnownTypeNames.SystemAttribute, ImmutableArray.Create("Attribute")),
                (WellKnownTypeNames.SystemEventArgs, ImmutableArray.Create("EventArgs")),
                (WellKnownTypeNames.SystemException, ImmutableArray.Create("Exception")),
                (WellKnownTypeNames.SystemCollectionsQueue, ImmutableArray.Create(QueueSuffix, CollectionSuffix)),
                (WellKnownTypeNames.SystemCollectionsStack, ImmutableArray.Create(StackSuffix, CollectionSuffix)),
                (WellKnownTypeNames.SystemCollectionsGenericQueue1, ImmutableArray.Create(QueueSuffix, CollectionSuffix)),
                (WellKnownTypeNames.SystemCollectionsGenericStack1, ImmutableArray.Create(StackSuffix, CollectionSuffix)),
                (WellKnownTypeNames.SystemDataDataSet, ImmutableArray.Create("DataSet")),
                (WellKnownTypeNames.SystemDataDataTable, ImmutableArray.Create("DataTable", CollectionSuffix)),
                (WellKnownTypeNames.SystemIOStream, ImmutableArray.Create("Stream")),
                // interfaces
                (WellKnownTypeNames.SystemCollectionsGenericIDictionary2, ImmutableArray.Create(DictionarySuffix, CollectionSuffix)),
                (WellKnownTypeNames.SystemCollectionsGenericIReadOnlyDictionary2, ImmutableArray.Create(DictionarySuffix, CollectionSuffix)),
                (WellKnownTypeNames.SystemCollectionsGenericISet1, ImmutableArray.Create(SetSuffix, CollectionSuffix)),
                (WellKnownTypeNames.SystemCollectionsGenericIReadOnlySet1, ImmutableArray.Create(SetSuffix, CollectionSuffix)),
                (WellKnownTypeNames.SystemCollectionsGenericICollection1, ImmutableArray.Create(CollectionSuffix, DictionarySuffix, SetSuffix, StackSuffix, QueueSuffix)),
                (WellKnownTypeNames.SystemCollectionsGenericIReadOnlyCollection1, ImmutableArray.Create(CollectionSuffix, DictionarySuffix, SetSuffix, StackSuffix, QueueSuffix)),
                (WellKnownTypeNames.SystemCollectionsIDictionary, ImmutableArray.Create(DictionarySuffix, CollectionSuffix)),
                (WellKnownTypeNames.SystemCollectionsICollection, ImmutableArray.Create(CollectionSuffix, DictionarySuffix, SetSuffix, StackSuffix, QueueSuffix)),
                (WellKnownTypeNames.SystemCollectionsIEnumerable, ImmutableArray.Create(CollectionSuffix, DictionarySuffix, SetSuffix, StackSuffix, QueueSuffix)),
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
                var wellKnownNamedType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(typeName);
                if (wellKnownNamedType == null || wellKnownNamedType.OriginalDefinition == null)
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
                if (!context.Options.MatchesConfiguredVisibility(OneSuffixRule, namedTypeSymbol, context.Compilation, context.CancellationToken))
                {
                    Debug.Assert(!context.Options.MatchesConfiguredVisibility(MultipleSuffixesRule, namedTypeSymbol, context.Compilation, context.CancellationToken));
                    Debug.Assert(!context.Options.MatchesConfiguredVisibility(UserSuffixRule, namedTypeSymbol, context.Compilation, context.CancellationToken));
                    return;
                }

                Debug.Assert(context.Options.MatchesConfiguredVisibility(MultipleSuffixesRule, namedTypeSymbol, context.Compilation, context.CancellationToken));
                Debug.Assert(context.Options.MatchesConfiguredVisibility(UserSuffixRule, namedTypeSymbol, context.Compilation, context.CancellationToken));

                var excludeIndirectBaseTypes = context.Options.GetBoolOptionValue(EditorConfigOptionNames.ExcludeIndirectBaseTypes, OneSuffixRule,
                   namedTypeSymbol, context.Compilation, defaultValue: true, cancellationToken: context.CancellationToken);

                var baseTypes = excludeIndirectBaseTypes
                    ? namedTypeSymbol.BaseType != null ? ImmutableArray.Create(namedTypeSymbol.BaseType) : ImmutableArray<INamedTypeSymbol>.Empty
                    : namedTypeSymbol.GetBaseTypes();

                var userTypeSuffixMap = context.Options.GetAdditionalRequiredSuffixesOption(UserSuffixRule, context.Symbol,
                    context.Compilation, context.CancellationToken);

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

            context.RegisterSymbolAction((saContext) =>
            {
                const string eventHandlerString = "EventHandler";
                var eventSymbol = (IEventSymbol)saContext.Symbol;
                if (!eventSymbol.Type.Name.EndsWith(eventHandlerString, StringComparison.Ordinal) &&
                    eventSymbol.Type.IsInSource() &&
                    eventSymbol.Type.TypeKind == TypeKind.Delegate &&
                    ((INamedTypeSymbol)eventSymbol.Type).DelegateInvokeMethod?.HasEventHandlerSignature(eventArgsType) == true)
                {
                    saContext.ReportDiagnostic(eventSymbol.CreateDiagnostic(OneSuffixRule, eventSymbol.Type.Name, eventHandlerString));
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
                if (userMap.TryGetValue(type.OriginalDefinition, out var suffix) &&
                    !RoslynString.IsNullOrWhiteSpace(suffix))
                {
                    suffixes = ImmutableArray.Create(suffix);
                    rule = UserSuffixRule;
                    return true;
                }

                if (hardcodedMap.TryGetValue(type.OriginalDefinition, out suffixes))
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