// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(
                (context) =>
                {
                    INamedTypeSymbol? iCollectionType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsICollection);
                    INamedTypeSymbol? genericICollectionType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericICollection1);
                    INamedTypeSymbol? arrayType = context.Compilation.GetSpecialType(SpecialType.System_Array);
                    INamedTypeSymbol? dataMemberAttribute = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationDataMemberAttribute);
                    ImmutableHashSet<INamedTypeSymbol> immutableInterfaces = GetIImmutableInterfaces(context.Compilation);

                    if (iCollectionType == null ||
                        genericICollectionType == null ||
                        arrayType == null)
                    {
                        return;
                    }

                    context.RegisterSymbolAction(c => AnalyzeSymbol(c, iCollectionType, genericICollectionType, arrayType, dataMemberAttribute, immutableInterfaces), SymbolKind.Property);
                });
        }

        public static void AnalyzeSymbol(
            SymbolAnalysisContext context,
            INamedTypeSymbol iCollectionType,
            INamedTypeSymbol genericICollectionType,
            INamedTypeSymbol arrayType,
            INamedTypeSymbol? dataMemberAttribute,
            ImmutableHashSet<INamedTypeSymbol> immutableInterfaces)
        {
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

            // make sure return type is NOT array
            if (Inherits(property.Type, arrayType))
            {
                return;
            }

            // make sure property type implements ICollection or ICollection<T>
            if (!Inherits(property.Type, iCollectionType) && !Inherits(property.Type, genericICollectionType))
            {
                return;
            }

            // exclude Immutable collections
            // see https://github.com/dotnet/roslyn-analyzers/issues/1900 for details
            if (!immutableInterfaces.IsEmpty &&
                property.Type.AllInterfaces.Any(i => immutableInterfaces.Contains(i.OriginalDefinition)))
            {
                return;
            }

            if (dataMemberAttribute != null)
            {
                // Special case: the DataContractSerializer requires that a public setter exists.
                bool hasDataMemberAttribute = property.GetAttributes().Any(a => a.AttributeClass.Equals(dataMemberAttribute));
                if (hasDataMemberAttribute)
                {
                    return;
                }
            }

            context.ReportDiagnostic(property.CreateDiagnostic(Rule, property.Name));
        }

        private static bool Inherits(ITypeSymbol symbol, ITypeSymbol baseType)
        {
            Debug.Assert(baseType.Equals(baseType.OriginalDefinition));
            return symbol?.OriginalDefinition.Inherits(baseType) ?? false;
        }

        private static ImmutableHashSet<INamedTypeSymbol> GetIImmutableInterfaces(Compilation compilation)
        {
            var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>();
            AddIfNotNull(compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsImmutableIImmutableDictionary));
            AddIfNotNull(compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsImmutableIImmutableList));
            AddIfNotNull(compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsImmutableIImmutableQueue));
            AddIfNotNull(compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsImmutableIImmutableSet));
            AddIfNotNull(compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsImmutableIImmutableStack));
            return builder.ToImmutable();

            // Local functions.
            void AddIfNotNull(INamedTypeSymbol? type)
            {
                if (type != null)
                {
                    builder.Add(type);
                }
            }
        }
    }
}