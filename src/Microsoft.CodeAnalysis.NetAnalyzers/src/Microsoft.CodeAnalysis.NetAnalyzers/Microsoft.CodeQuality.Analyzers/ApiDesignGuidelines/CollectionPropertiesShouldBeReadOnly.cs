// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.Lightup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA2227: Collection properties should be read only
    ///
    /// Cause:
    /// An externally visible writable property is a type that implements System.Collections.ICollection.
    /// Arrays, indexers(properties with the name 'Item'), and permission sets are ignored by the rule.
    ///
    /// Description:
    /// A writable collection property allows a user to replace the collection with a completely different collection.
    /// A read-only property stops the collection from being replaced but still allows the individual members to be set.
    /// If replacing the collection is a goal, the preferred design pattern is to include a method to remove all the elements
    /// from the collection and a method to re-populate the collection.See the Clear and AddRange methods of the System.Collections.ArrayList class
    /// for an example of this pattern.
    ///
    /// Both binary and XML serialization support read-only properties that are collections.
    /// The System.Xml.Serialization.XmlSerializer class has specific requirements for types that implement ICollection and
    /// System.Collections.IEnumerable in order to be serializable.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class CollectionPropertiesShouldBeReadOnlyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2227";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.CollectionPropertiesShouldBeReadOnlyTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.CollectionPropertiesShouldBeReadOnlyMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.CollectionPropertiesShouldBeReadOnlyDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                    s_localizableTitle,
                                                                    s_localizableMessage,
                                                                    DiagnosticCategory.Usage,
                                                                    RuleLevel.Disabled, // Guidance needs to be improved to be more clear
                                                                    description: s_localizableDescription,
                                                                    isPortedFxCopRule: true,
                                                                    isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(
                (context) =>
                {
                    var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
                    var knownTypes = new KnownTypes(wellKnownTypeProvider);

                    if (knownTypes.ICollectionType == null ||
                        knownTypes.GenericICollectionType == null ||
                        knownTypes.ArrayType == null)
                    {
                        return;
                    }

                    context.RegisterSymbolAction(c => AnalyzeSymbol(c, knownTypes), SymbolKind.Property);
                });
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, KnownTypes knownTypes)
        {
            RoslynDebug.Assert(knownTypes.ICollectionType != null &&
                knownTypes.GenericICollectionType != null &&
                knownTypes.ArrayType != null);

            var property = (IPropertySymbol)context.Symbol;

            // check whether it has a public setter
            IMethodSymbol setter = property.SetMethod;
            if (setter == null || !setter.IsExternallyVisible())
            {
                return;
            }

            // make sure this property is NOT an indexer
            if (property.IsIndexer)
            {
                return;
            }

            // make sure this property is NOT an init
            if (setter.IsInitOnly())
            {
                return;
            }

            // make sure return type is NOT array
            if (Inherits(property.Type, knownTypes.ArrayType))
            {
                return;
            }

            // make sure property type implements ICollection or ICollection<T>
            if (!Inherits(property.Type, knownTypes.ICollectionType) && !Inherits(property.Type, knownTypes.GenericICollectionType))
            {
                return;
            }

            // exclude Immutable collections
            // see https://github.com/dotnet/roslyn-analyzers/issues/1900 for details
            if (!knownTypes.ImmutableInterfaces.IsEmpty &&
                property.Type.AllInterfaces.Any(i => knownTypes.ImmutableInterfaces.Contains(i.OriginalDefinition)))
            {
                return;
            }

            // exclude readonly collections
            if (property.Type.OriginalDefinition.Equals(knownTypes.ReadonlyCollection) ||
                property.Type.OriginalDefinition.Equals(knownTypes.ReadonlyDictionary) ||
                property.Type.OriginalDefinition.Equals(knownTypes.ReadonlyObservableCollection))
            {
                return;
            }

            // Special case: the DataContractSerializer requires that a public setter exists.
            if (property.HasAttribute(knownTypes.DataMemberAttribute))
            {
                return;
            }

            context.ReportDiagnostic(property.CreateDiagnostic(Rule, property.Name));
        }

        private static bool Inherits(ITypeSymbol symbol, ITypeSymbol baseType)
        {
            Debug.Assert(baseType.Equals(baseType.OriginalDefinition));
            return symbol?.OriginalDefinition.Inherits(baseType) ?? false;
        }

        private sealed class KnownTypes
        {
            public KnownTypes(WellKnownTypeProvider wellKnownTypeProvider)
            {
                ICollectionType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsICollection);
                GenericICollectionType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericICollection1);
                ArrayType = wellKnownTypeProvider.Compilation.GetSpecialType(SpecialType.System_Array);
                DataMemberAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationDataMemberAttribute);
                ImmutableInterfaces = GetIImmutableInterfaces(wellKnownTypeProvider);
                ReadonlyCollection = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsObjectModelReadOnlyCollection1);
                ReadonlyDictionary = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsObjectModelReadOnlyDictionary2);
                ReadonlyObservableCollection = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsObjectModelReadOnlyObservableCollection1);
            }

            public INamedTypeSymbol? ICollectionType { get; }
            public INamedTypeSymbol? GenericICollectionType { get; }
            public INamedTypeSymbol? ArrayType { get; }
            public INamedTypeSymbol? DataMemberAttribute { get; }
            public ImmutableHashSet<INamedTypeSymbol> ImmutableInterfaces { get; }
            public INamedTypeSymbol? ReadonlyCollection { get; }
            public INamedTypeSymbol? ReadonlyDictionary { get; }
            public INamedTypeSymbol? ReadonlyObservableCollection { get; }

            private static ImmutableHashSet<INamedTypeSymbol> GetIImmutableInterfaces(WellKnownTypeProvider wellKnownTypeProvider)
            {
                var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>();
                builder.AddIfNotNull(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsImmutableIImmutableDictionary2));
                builder.AddIfNotNull(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsImmutableIImmutableList1));
                builder.AddIfNotNull(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsImmutableIImmutableQueue1));
                builder.AddIfNotNull(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsImmutableIImmutableSet1));
                builder.AddIfNotNull(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsImmutableIImmutableStack1));
                return builder.ToImmutable();
            }
        }
    }
}
