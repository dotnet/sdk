// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
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

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(
               (context) =>
               {
                   INamedTypeSymbol? iCollectionType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsICollection);
                   INamedTypeSymbol? genericICollectionType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericICollection1);
                   INamedTypeSymbol? iEnumerableType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsIEnumerable);
                   INamedTypeSymbol? genericIEnumerableType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIEnumerable1);
                   INamedTypeSymbol? iListType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsIList);
                   INamedTypeSymbol? genericIListType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIList1);

                   if (iCollectionType == null && genericICollectionType == null &&
                       iEnumerableType == null && genericIEnumerableType == null &&
                       iListType == null && genericIListType == null)
                   {
                       return;
                   }

                   context.RegisterSymbolAction(c => AnalyzeSymbol(c,
                                                iCollectionType, genericICollectionType,
                                                iEnumerableType, genericIEnumerableType,
                                                iListType, genericIListType),
                                                SymbolKind.NamedType);
               });
        }

        private static void AnalyzeSymbol(
            SymbolAnalysisContext context,
            INamedTypeSymbol? iCollectionType,
            INamedTypeSymbol? gCollectionType,
            INamedTypeSymbol? iEnumerableType,
            INamedTypeSymbol? gEnumerableType,
            INamedTypeSymbol? iListType,
            INamedTypeSymbol? gListType)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // FxCop compat: only fire on externally visible types by default.
            if (!namedTypeSymbol.MatchesConfiguredVisibility(context.Options, Rule, context.CancellationToken))
            {
                return;
            }

            var allInterfacesStatus = default(CollectionsInterfaceStatus);
            foreach (var @interface in namedTypeSymbol.AllInterfaces)
            {
                var originalDefinition = @interface.OriginalDefinition;
                if (originalDefinition.Equals(iCollectionType))
                {
                    allInterfacesStatus.ICollectionPresent = true;
                }
                else if (originalDefinition.Equals(iEnumerableType))
                {
                    allInterfacesStatus.IEnumerablePresent = true;
                }
                else if (originalDefinition.Equals(iListType))
                {
                    allInterfacesStatus.IListPresent = true;
                }
                else if (originalDefinition.Equals(gCollectionType))
                {
                    allInterfacesStatus.GenericICollectionPresent = true;
                }
                else if (originalDefinition.Equals(gEnumerableType))
                {
                    allInterfacesStatus.GenericIEnumerablePresent = true;
                }
                else if (originalDefinition.Equals(gListType))
                {
                    allInterfacesStatus.GenericIListPresent = true;
                }
            }

            INamedTypeSymbol? missingInterface;
            INamedTypeSymbol? implementedInterface;
            if (allInterfacesStatus.GenericIListPresent)
            {
                // Implemented IList<T>, meaning has all 3 generic interfaces. Nothing can be wrong.
                return;
            }
            else if (allInterfacesStatus.IListPresent)
            {
                // Implemented IList but not IList<T>.
                missingInterface = gListType;
                implementedInterface = iListType;
            }
            else if (allInterfacesStatus.GenericICollectionPresent)
            {
                // Implemented ICollection<T>, and doesn't have an inherit of IList. Nothing can be wrong
                return;
            }
            else if (allInterfacesStatus.ICollectionPresent)
            {
                // Implemented ICollection but not ICollection<T>
                missingInterface = gCollectionType;
                implementedInterface = iCollectionType;
            }
            else if (allInterfacesStatus.GenericIEnumerablePresent)
            {
                // Implemented IEnumerable<T>, and doesn't have an inherit of ICollection. Nothing can be wrong
                return;
            }
            else if (allInterfacesStatus.IEnumerablePresent)
            {
                // Implemented IEnumerable, but not IEnumerable<T>
                missingInterface = gEnumerableType;
                implementedInterface = iEnumerableType;
            }
            else
            {
                // No collections implementation, nothing can be wrong.
                return;
            }

            RoslynDebug.Assert(missingInterface != null && implementedInterface != null);
            context.ReportDiagnostic(Diagnostic.Create(Rule,
                                                       namedTypeSymbol.Locations.First(),
                                                       namedTypeSymbol.Name,
                                                       implementedInterface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                                                       missingInterface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }

#pragma warning disable CA1815 // Override equals and operator equals on value types
        private struct CollectionsInterfaceStatus
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            public bool IListPresent { get; set; }
            public bool GenericIListPresent { get; set; }
            public bool ICollectionPresent { get; set; }
            public bool GenericICollectionPresent { get; set; }
            public bool IEnumerablePresent { get; set; }
            public bool GenericIEnumerablePresent { get; set; }
        }
    }
}
