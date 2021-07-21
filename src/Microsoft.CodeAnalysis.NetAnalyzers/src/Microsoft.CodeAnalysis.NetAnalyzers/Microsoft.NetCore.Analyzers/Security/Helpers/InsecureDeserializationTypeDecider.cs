// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;

namespace Microsoft.NetCore.Analyzers.Security.Helpers
{
    /// <summary>
    /// Determines if a given type is insecure for deserialization, by seeing if it contain known dangerous types.
    /// </summary>
    internal sealed partial class InsecureDeserializationTypeDecider
    {
        private static readonly string[] InsecureTypeNames =
        {
            WellKnownTypeNames.SystemDataDataSet,
            WellKnownTypeNames.SystemDataDataTable,
        };

        private static readonly BoundedCacheWithFactory<Compilation, InsecureDeserializationTypeDecider> BoundedCache =
            new();

        /// <summary>
        /// Gets a cached <see cref="InsecureDeserializationTypeDecider"/> for the given compilation.
        /// </summary>
        /// <param name="compilation">Compilation that the decider is for.</param>
        /// <returns>Cached decider.</returns>
        public static InsecureDeserializationTypeDecider GetOrCreate(Compilation compilation)
        {
            return BoundedCache.GetOrCreateValue(compilation, Create);

            // Local functions.
            static InsecureDeserializationTypeDecider Create(Compilation c) => new(c);
        }

        /// <summary>
        /// Constructs.
        /// </summary>
        /// <param name="compilation">Compilation being analyzed.</param>
        private InsecureDeserializationTypeDecider(Compilation compilation)
        {
            foreach (string typeName in InsecureTypeNames)
            {
                if (compilation.TryGetOrCreateTypeByMetadataName(typeName, out INamedTypeSymbol? namedTypeSymbol))
                {
                    this.InsecureTypeSymbols.Add(namedTypeSymbol);
                }
            }

            this.SymbolByDisplayStringComparer = new SymbolByDisplayStringComparer(compilation);
            this.WellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilation);

            this.GeneratedCodeAttributeTypeSymbol = this.WellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemCodeDomCompilerGeneratedCodeAttribute);

            this.SerializableAttributeTypeSymbol = this.WellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemSerializableAttribute);
            this.NonSerializedAttributeTypeSymbol = this.WellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemSerializableAttribute);

            this.DataContractAttributeTypeSymbol = this.WellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemRuntimeSerializationDataContractAttribute);
            this.DataMemberAttributeTypeSymbol = this.WellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemRuntimeSerializationDataMemberAttribute);
            this.IgnoreDataMemberTypeSymbol = this.WellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemRuntimeSerializationIgnoreDataMemberAttribute);
            this.KnownTypeAttributeTypeSymbol = this.WellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemRuntimeSerializationKnownTypeAttribute);
            this.XmlSerializationAttributeTypes = new XmlSerializationAttributeTypes(
                this.WellKnownTypeProvider);
            this.JsonIgnoreAttributeTypeSymbol = this.WellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.NewtonsoftJsonJsonIgnoreAttribute);
        }

        /// <summary>
        /// Doesn't construct.
        /// </summary>
        private InsecureDeserializationTypeDecider()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Comparer for the compilation's TypeSymbols.
        /// </summary>
        public SymbolByDisplayStringComparer SymbolByDisplayStringComparer { get; }

        /// <summary>
        /// Type cache.
        /// </summary>
        private WellKnownTypeProvider WellKnownTypeProvider { get; }

        /// <summary>
        /// Set of type symbols for types that are insecure if deserialized.
        /// </summary>
        private HashSet<ITypeSymbol> InsecureTypeSymbols { get; } = new HashSet<ITypeSymbol>();

        private INamedTypeSymbol? GeneratedCodeAttributeTypeSymbol { get; }
        private INamedTypeSymbol? SerializableAttributeTypeSymbol { get; }
        private INamedTypeSymbol? NonSerializedAttributeTypeSymbol { get; }
        private INamedTypeSymbol? DataContractAttributeTypeSymbol { get; }
        private INamedTypeSymbol? DataMemberAttributeTypeSymbol { get; }
        private INamedTypeSymbol? IgnoreDataMemberTypeSymbol { get; }
        private INamedTypeSymbol? KnownTypeAttributeTypeSymbol { get; }
        private XmlSerializationAttributeTypes XmlSerializationAttributeTypes { get; }
        private INamedTypeSymbol? JsonIgnoreAttributeTypeSymbol { get; }

        // Cache results for IsTypeInsecure()
        // Key: typeSymbol in IsTypeInsecure()
        // Value: insecureTypeSymbol in IsTypeInsecure()
        private readonly ConcurrentDictionary<ITypeSymbol, ITypeSymbol?> IsTypeInsecureCache =
            new();

        /// <summary>
        /// Determines if the given type is insecure when deserialized, without looking at its child fields and properties.
        /// </summary>
        /// <param name="typeSymbol">Type to check.</param>
        /// <param name="insecureTypeSymbol">Insecure type, if the checked type is insecure.</param>
        /// <returns>True if insecure, false otherwise.</returns>
        /// <remarks>This only considers the type and its associated types (generic type arguments, base classes, etc), not
        /// types of member fields and properties.</remarks>
        public bool IsTypeInsecure(
            ITypeSymbol? typeSymbol,
            [NotNullWhen(returnValue: true)] out ITypeSymbol? insecureTypeSymbol)
        {
            insecureTypeSymbol = null;

            if (typeSymbol == null || this.InsecureTypeSymbols.Count == 0)
            {
                return false;
            }

            insecureTypeSymbol = this.IsTypeInsecureCache.GetOrAdd(typeSymbol, Compute(typeSymbol));
            return insecureTypeSymbol != null;

            // Local functions.
            ITypeSymbol? Compute(ITypeSymbol typeSymbol)
            {
                // Sort type symbols by display string so that we get consistent results.
                SortedSet<ITypeSymbol> associatedTypeSymbols = new SortedSet<ITypeSymbol>(
                    this.SymbolByDisplayStringComparer);
                GetAssociatedTypes(typeSymbol, associatedTypeSymbols);
                foreach (ITypeSymbol t in associatedTypeSymbols)
                {
                    if (this.InsecureTypeSymbols.Contains(t))
                    {
                        return t;
                    }
                }

                return null;
            }
        }

        // Cache for IsObjectGraphInsecure results.
        // Key: (rootType, options) arguments in IsObjectGraphInsecure()
        // Value: results argument in IsObjectGraphInsecure().
        private readonly ConcurrentDictionary<(ITypeSymbol, ObjectGraphOptions), ImmutableArray<InsecureObjectGraphResult>> IsObjectGraphInsecureCache =
            new();

        /// <summary>
        /// Determines if a type's object graph contains an insecure type, by walking through its serializable members.
        /// </summary>
        /// <param name="rootType">Type to check.</param>
        /// <param name="options">Options for the type of serialization.</param>
        /// <param name="results">List to populate results of which symbols (fields or properties) are an insecure
        /// type.</param>
        /// <returns>True if are any insecure symbols, false otherwise.</returns>
        [SuppressMessage("Style", "IDE0047:Remove unnecessary parentheses", Justification = "Group related conditions together.")]
        public bool IsObjectGraphInsecure(
            ITypeSymbol rootType,
            ObjectGraphOptions options,
            out ImmutableArray<InsecureObjectGraphResult> results)
        {
            options.ThrowIfInvalid(nameof(options));

            if (this.InsecureTypeSymbols.Count == 0)
            {
                results = ImmutableArray<InsecureObjectGraphResult>.Empty;
                return false;
            }

            results = this.IsObjectGraphInsecureCache.GetOrAdd((rootType, options), Compute);
            return !results.IsEmpty;

            // Local functions.
            ImmutableArray<InsecureObjectGraphResult> Compute((ITypeSymbol, ObjectGraphOptions) _)
            {
                ImmutableArray<InsecureObjectGraphResult>.Builder resultBuilder =
                    ImmutableArray.CreateBuilder<InsecureObjectGraphResult>();

                using PooledHashSet<ITypeSymbol> visitedTypes = PooledHashSet<ITypeSymbol>.GetInstance();
                GetInsecureSymbol(rootType, visitedTypes, resultBuilder);

                return resultBuilder.ToImmutable();
            }

            void GetInsecureSymbol(
                ITypeSymbol typeSymbol,
                PooledHashSet<ITypeSymbol> visitedTypes,
                ImmutableArray<InsecureObjectGraphResult>.Builder resultBuilder)
            {
                if (!visitedTypes.Add(typeSymbol))
                {
                    return;
                }

                if (this.IsTypeInsecure(typeSymbol, out ITypeSymbol? typeInsecureTypeSymbol))
                {
                    resultBuilder.Add(new InsecureObjectGraphResult(typeSymbol, null, null, typeInsecureTypeSymbol));
                }

                bool[] hasAttributes = typeSymbol.HasAttributes(
                    this.GeneratedCodeAttributeTypeSymbol,
                    this.SerializableAttributeTypeSymbol,
                    this.DataContractAttributeTypeSymbol,
                    this.KnownTypeAttributeTypeSymbol);
                int index = 0;
                bool hasGeneratedCodeAttribute = hasAttributes[index++];
                bool hasSerializableAttribute = hasAttributes[index++];
                bool hasDataContractAttribute = hasAttributes[index++];
                bool hasKnownTypeAttribute = hasAttributes[index++];

                bool hasAnyIgnoreDataMemberAttribute =
                    typeSymbol.GetMembers().Any(m => m.HasAttribute(this.IgnoreDataMemberTypeSymbol));

                bool hasAnyXmlSerializationAttributes =
                    this.XmlSerializationAttributeTypes.HasAnyAttribute(typeSymbol)
                        || typeSymbol.GetMembers().Any(m => this.XmlSerializationAttributeTypes.HasAnyAttribute(m));

                // Consider handling other Newtonsoft Json.NET member serialization modes other than its default.

                // Sort type symbols by display strings.
                // Keep track of member types we see, and we'll recurse through those afterwards.
                SortedSet<ITypeSymbol> typesToRecurse = new SortedSet<ITypeSymbol>(this.SymbolByDisplayStringComparer);
                foreach (ISymbol member in typeSymbol.GetMembers())
                {
                    switch (member)
                    {
                        case IFieldSymbol fieldSymbol:
                            if (!fieldSymbol.IsStatic
                                && !fieldSymbol.IsBackingFieldForProperty(out _) // Handle properties below.
                                && ((options.BinarySerialization
                                        && hasSerializableAttribute
                                        && !fieldSymbol.HasAttribute(this.NonSerializedAttributeTypeSymbol))
                                    || (options.DataContractSerialization
                                         && ((hasDataContractAttribute && fieldSymbol.HasAttribute(this.DataMemberAttributeTypeSymbol))
                                            || (!hasDataContractAttribute && !fieldSymbol.HasAttribute(this.IgnoreDataMemberTypeSymbol))))
                                    || (options.XmlSerialization
                                            && !fieldSymbol.HasAttribute(
                                                    this.XmlSerializationAttributeTypes.XmlIgnoreAttribute)
                                            && fieldSymbol.DeclaredAccessibility == Accessibility.Public)
                                    || (options.JavaScriptSerializer
                                            && fieldSymbol.DeclaredAccessibility == Accessibility.Public)
                                    || (options.NewtonsoftJsonNetSerialization
                                            && fieldSymbol.DeclaredAccessibility == Accessibility.Public
                                            && !fieldSymbol.HasAttribute(this.JsonIgnoreAttributeTypeSymbol)
                                            && !fieldSymbol.HasAttribute(this.NonSerializedAttributeTypeSymbol))))
                            {
                                if (this.IsTypeInsecure(fieldSymbol.Type, out ITypeSymbol? fieldInsecureTypeSymbol))
                                {
                                    resultBuilder.Add(
                                        new InsecureObjectGraphResult(
                                            fieldSymbol,
                                            null,
                                            null,
                                            fieldInsecureTypeSymbol));
                                }
                                else
                                {
                                    typesToRecurse.Add(fieldSymbol.Type);
                                }
                            }

                            break;

                        case IPropertySymbol propertySymbol:
                            if (!propertySymbol.IsStatic
                                && ((options.BinarySerialization
                                        && hasSerializableAttribute
                                        && !propertySymbol.HasAttribute(this.NonSerializedAttributeTypeSymbol)
                                        && propertySymbol.IsPropertyWithBackingField()
                                        )
                                    || (options.DataContractSerialization
                                        && ((hasDataContractAttribute && propertySymbol.HasAttribute(this.DataMemberAttributeTypeSymbol))
                                            || (!hasDataContractAttribute && !propertySymbol.HasAttribute(this.IgnoreDataMemberTypeSymbol)))
                                        && propertySymbol.GetMethod != null
                                        && propertySymbol.SetMethod != null)
                                    || (options.XmlSerialization
                                        && !propertySymbol.HasAttribute(this.XmlSerializationAttributeTypes.XmlIgnoreAttribute)
                                        && propertySymbol.DeclaredAccessibility == Accessibility.Public
                                        && propertySymbol.GetMethod != null
                                        && propertySymbol.GetMethod.DeclaredAccessibility == Accessibility.Public
                                        && propertySymbol.SetMethod != null
                                        && propertySymbol.SetMethod.DeclaredAccessibility == Accessibility.Public)
                                    || (options.JavaScriptSerializer
                                        && propertySymbol.DeclaredAccessibility == Accessibility.Public
                                        && propertySymbol.SetMethod != null
                                        && propertySymbol.SetMethod.DeclaredAccessibility == Accessibility.Public)
                                    || (options.NewtonsoftJsonNetSerialization
                                        && propertySymbol.DeclaredAccessibility == Accessibility.Public
                                        && !propertySymbol.HasAttribute(this.JsonIgnoreAttributeTypeSymbol)
                                        && !propertySymbol.HasAttribute(this.NonSerializedAttributeTypeSymbol))))
                            {
                                if (this.IsTypeInsecure(propertySymbol.Type, out ITypeSymbol? propertyInsecureTypeSymbol))
                                {
                                    resultBuilder.Add(
                                        new InsecureObjectGraphResult(
                                            propertySymbol,
                                            null,
                                            null,
                                            propertyInsecureTypeSymbol));
                                }
                                else
                                {
                                    typesToRecurse.Add(propertySymbol.Type);
                                }
                            }

                            break;
                    }
                }

                if (options.DataContractSerialization)
                {
                    // Look through [KnownType(typeof(Whatev))] attributes.
                    foreach (AttributeData knownTypeAttributeData in typeSymbol.GetAttributes(this.KnownTypeAttributeTypeSymbol))
                    {
                        if (knownTypeAttributeData.AttributeConstructor.Parameters.Length != 1
                            || knownTypeAttributeData.ConstructorArguments.Length != 1)
                        {
                            continue;
                        }

                        var typedConstant = knownTypeAttributeData.ConstructorArguments[0];
                        if (typedConstant.Kind != TypedConstantKind.Type    // Not handling the string methodName overload
                            || typedConstant.Value is not ITypeSymbol typedConstantTypeSymbol)
                        {
                            continue;
                        }

                        if (this.IsTypeInsecure(typedConstantTypeSymbol, out ITypeSymbol? knownTypeInsecureType))
                        {
                            resultBuilder.Add(
                                new InsecureObjectGraphResult(
                                    null,
                                    knownTypeAttributeData,
                                    typedConstant,
                                    knownTypeInsecureType));
                        }
                        else
                        {
                            typesToRecurse.Add(typedConstantTypeSymbol);
                        }
                    }
                }

                if (options.XmlSerialization)
                {
                    // Look through [XmlInclude(typeof(Whatev))] attributes.
                    foreach (AttributeData xmlIncludeAttributeData
                        in typeSymbol.GetAttributes(this.XmlSerializationAttributeTypes.XmlIncludeAttribute))
                    {
                        if (xmlIncludeAttributeData.AttributeConstructor.Parameters.Length != 1
                          || xmlIncludeAttributeData.ConstructorArguments.Length != 1)
                        {
                            continue;
                        }

                        var typedConstant = xmlIncludeAttributeData.ConstructorArguments[0];
                        if (typedConstant.Kind != TypedConstantKind.Type
                          || typedConstant.Value is not ITypeSymbol typedConstantTypeSymbol)
                        {
                            continue;
                        }

                        if (this.IsTypeInsecure(typedConstantTypeSymbol, out ITypeSymbol? xmlIncludeInsecureType))
                        {
                            resultBuilder.Add(
                                new InsecureObjectGraphResult(
                                    null,
                                    xmlIncludeAttributeData,
                                    typedConstant,
                                    xmlIncludeInsecureType));
                        }
                        else
                        {
                            typesToRecurse.Add(typedConstantTypeSymbol);
                        }
                    }
                }

                if (options.Recurse)
                {
                    foreach (ITypeSymbol memberTypeSymbol in typesToRecurse)
                    {
                        GetInsecureSymbol(memberTypeSymbol, visitedTypes, resultBuilder);
                    }
                }
            }
        }

        /// <summary>
        /// Gets "associated" types, e.g. "List&lt;Foo&lt;Bar[]&gt;&gt;" means "List&lt;T&gt;", "Foo&lt;T&gt;", and "Bar".
        /// </summary>
        /// <param name="type">Type to get associated types for.</param>
        /// <param name="results">Set to populate with associated types.</param>
        private static void GetAssociatedTypes(
            ITypeSymbol type,
            SortedSet<ITypeSymbol> results)
        {
            if (type == null || !results.Add(type))
            {
                return;
            }

            if (type is INamedTypeSymbol namedTypeSymbol)
            {
                // 1. Type arguments of generic type.
                if (namedTypeSymbol.IsGenericType)
                {
                    foreach (ITypeSymbol? arg in namedTypeSymbol.TypeArguments)
                    {
                        GetAssociatedTypes(arg, results);
                    }
                }

                // 2. The type it constructed from.
                GetAssociatedTypes(namedTypeSymbol.ConstructedFrom, results);
            }
            else if (type is IArrayTypeSymbol arrayTypeSymbol)
            {
                // 3. Element type of the array.
                GetAssociatedTypes(arrayTypeSymbol.ElementType, results);
            }

            // 4. Base type.
            GetAssociatedTypes(type.BaseType, results);
        }
    }
}
