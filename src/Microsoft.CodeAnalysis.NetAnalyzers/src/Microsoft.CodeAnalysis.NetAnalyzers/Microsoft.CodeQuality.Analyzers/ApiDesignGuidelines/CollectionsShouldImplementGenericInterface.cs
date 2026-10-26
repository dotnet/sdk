// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Analyzer.Utilities.PooledObjects.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1010: <inheritdoc cref="CollectionsShouldImplementGenericInterfaceTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class CollectionsShouldImplementGenericInterfaceAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1010";

        private static readonly LocalizableString s_localizableTitle =
            CreateLocalizableResourceString(nameof(CollectionsShouldImplementGenericInterfaceTitle));

        private static readonly LocalizableString s_localizableDescription =
            CreateLocalizableResourceString(nameof(CollectionsShouldImplementGenericInterfaceDescription));

        internal static readonly DiagnosticDescriptor Rule =
            DiagnosticDescriptorHelper.Create(
                RuleId,
                s_localizableTitle,
                CreateLocalizableResourceString(nameof(CollectionsShouldImplementGenericInterfaceMessage)),
                DiagnosticCategory.Design,
                RuleLevel.IdeHidden_BulkConfigurable,
                description: s_localizableDescription,
                isPortedFxCopRule: true,
                isDataflowRule: false);

        internal static readonly DiagnosticDescriptor RuleMultiple =
            DiagnosticDescriptorHelper.Create(
                RuleId,
                s_localizableTitle,
                CreateLocalizableResourceString(nameof(CollectionsShouldImplementGenericInterfaceMultipleMessage)),
                DiagnosticCategory.Design,
                RuleLevel.IdeHidden_BulkConfigurable,
                description: s_localizableDescription,
                isPortedFxCopRule: true,
                isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(
               context =>
               {
                   var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);

                   // Orders inside the array matters as we report only on the first missing implemented tuple.
                   var interfaceToGenericInterfaceMapBuilder = ImmutableArray.CreateBuilder<KeyValuePair<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>>();

                   if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsIList, out var iListType))
                   {
                       var builder = ArrayBuilder<INamedTypeSymbol>.GetInstance();
                       builder.AddIfNotNull(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIList1));
                       builder.AddIfNotNull(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIReadOnlyList1));
                       if (builder.Count > 0)
                       {
                           interfaceToGenericInterfaceMapBuilder.Add(new(iListType, builder.ToImmutable()));
                       }
                   }

                   if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsICollection, out var iCollectionType))
                   {
                       var builder = ArrayBuilder<INamedTypeSymbol>.GetInstance();
                       builder.AddIfNotNull(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericICollection1));
                       builder.AddIfNotNull(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIReadOnlyCollection1));
                       if (builder.Count > 0)
                       {
                           interfaceToGenericInterfaceMapBuilder.Add(new(iCollectionType, builder.ToImmutable()));
                       }
                   }

                   if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsIEnumerable, out var iEnumerableType) &&
                       wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIEnumerable1, out var genericIEnumerableType))
                   {
                       interfaceToGenericInterfaceMapBuilder.Add(new(iEnumerableType, ImmutableArray.Create(genericIEnumerableType)));
                   }

                   context.RegisterSymbolAction(c => AnalyzeSymbol(c, interfaceToGenericInterfaceMapBuilder.ToImmutable()), SymbolKind.NamedType);
               });
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, ImmutableArray<KeyValuePair<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>> interfacePairs)
        {
            Debug.Assert(interfacePairs.All(kvp => kvp.Key.TypeKind == TypeKind.Interface && kvp.Value.All(x => x.TypeKind == TypeKind.Interface)));

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

                if (allInterfaces.Contains(kvp.Key) && !allInterfaces.Intersect(kvp.Value, SymbolEqualityComparer.Default).Any())
                {
                    if (kvp.Value.Length > 1)
                    {
                        ReportDiagnosticAlt(kvp.Key, kvp.Value);
                    }
                    else
                    {
                        ReportDiagnostic(kvp.Key, kvp.Value[0]);
                    }

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

            void ReportDiagnosticAlt(INamedTypeSymbol @interface, IEnumerable<INamedTypeSymbol> genericInterfaces)
            {
                context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(RuleMultiple, namedTypeSymbol.Name,
                    @interface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    string.Join("', '", genericInterfaces.Select(x => x.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)))));
            }
        }
    }
}
