// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1010: Collections should implement generic interface
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class CollectionsShouldImplementGenericInterfaceAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1010";

        private static readonly LocalizableString s_localizableTitle =
            new LocalizableResourceString(
                nameof(MicrosoftCodeQualityAnalyzersResources.CollectionsShouldImplementGenericInterfaceTitle),
                MicrosoftCodeQualityAnalyzersResources.ResourceManager,
                typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableStandardMessage =
            new LocalizableResourceString(
                nameof(MicrosoftCodeQualityAnalyzersResources.CollectionsShouldImplementGenericInterfaceMessage),
                MicrosoftCodeQualityAnalyzersResources.ResourceManager,
                typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableDescription =
            new LocalizableResourceString(
                nameof(MicrosoftCodeQualityAnalyzersResources.CollectionsShouldImplementGenericInterfaceDescription),
                MicrosoftCodeQualityAnalyzersResources.ResourceManager,
                typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule =
            DiagnosticDescriptorHelper.Create(
                RuleId,
                s_localizableTitle,
                s_localizableStandardMessage,
                DiagnosticCategory.Design,
                RuleLevel.IdeHidden_BulkConfigurable,
                description: s_localizableDescription,
                isPortedFxCopRule: true,
                isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(
               context =>
               {
                   var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);

                   // Orders inside the array matters as we report only on the first missing implemented tuple.
                   var interfaceToGenericInterfaceMapBuilder = ImmutableArray.CreateBuilder<KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>>();

                   INamedTypeSymbol? iListType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsIList);
                   INamedTypeSymbol? genericIListType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIList1);

                   if (iListType != null && genericIListType != null)
                   {
                       interfaceToGenericInterfaceMapBuilder.Add(new KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>(iListType, genericIListType));
                   }

                   INamedTypeSymbol? iCollectionType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsICollection);
                   INamedTypeSymbol? genericICollectionType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericICollection1);

                   if (iCollectionType != null && genericICollectionType != null)
                   {
                       interfaceToGenericInterfaceMapBuilder.Add(new KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>(iCollectionType, genericICollectionType));
                   }

                   INamedTypeSymbol? iEnumerableType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsIEnumerable);
                   INamedTypeSymbol? genericIEnumerableType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIEnumerable1);

                   if (iEnumerableType != null && genericIEnumerableType != null)
                   {
                       interfaceToGenericInterfaceMapBuilder.Add(new KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>(iEnumerableType, genericIEnumerableType));
                   }

                   context.RegisterSymbolAction(c => AnalyzeSymbol(c, interfaceToGenericInterfaceMapBuilder.ToImmutable()), SymbolKind.NamedType);
               });
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, ImmutableArray<KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>> interfacePairs)
        {
            Debug.Assert(interfacePairs.All(kvp => kvp.Key.TypeKind == TypeKind.Interface && kvp.Value.TypeKind == TypeKind.Interface));

            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // FxCop compat: only fire on externally visible types by default.
            if (!context.Options.MatchesConfiguredVisibility(Rule, namedTypeSymbol, context.Compilation))
            {
                return;
            }

            using var allInterfaces = PooledHashSet<INamedTypeSymbol>.GetInstance();
            foreach (var @interface in namedTypeSymbol.AllInterfaces.Select(i => i.OriginalDefinition))
            {
                allInterfaces.Add(@interface);
            }

            // First we need to try to match all types from the user definition...
            var userMap = context.Options.GetAdditionalRequiredGenericInterfaces(Rule, context.Symbol, context.Compilation);
            if (!userMap.IsEmpty)
            {
                foreach (var @interface in allInterfaces)
                {
                    if (!@interface.IsGenericType &&
                        userMap.TryGetValue(@interface, out var genericInterface) &&
                        genericInterface?.IsGenericType == true)
                    {
                        ReportDiagnostic(@interface, genericInterface);
                        return;
                    }
                }
            }

            // ...Then we can proceed with the hardcoded ones keeping the declaration order
            for (int i = 0; i < interfacePairs.Length; i++)
            {
                var kvp = interfacePairs[i];

                if (allInterfaces.Contains(kvp.Key) && !allInterfaces.Contains(kvp.Value))
                {
                    ReportDiagnostic(kvp.Key, kvp.Value);
                    return;
                }
            }

            return;

            void ReportDiagnostic(INamedTypeSymbol @interface, INamedTypeSymbol genericInterface)
            {
                context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(Rule, namedTypeSymbol.Name,
                    @interface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    genericInterface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }
    }
}
