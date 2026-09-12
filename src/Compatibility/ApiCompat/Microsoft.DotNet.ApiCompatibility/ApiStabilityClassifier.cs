// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.ApiCompatibility
{
    internal static class ApiStabilityClassifier
    {
        private const string ExperimentalAttributeName = "ExperimentalAttribute";

        private static readonly ConditionalWeakTable<ISymbol, CacheEntry> s_classifications = new();

        public static ApiStability Classify(ISymbol? symbol, ISymbolFilter attributeDataSymbolFilter)
        {
            if (symbol is null)
            {
                return ApiStability.Stable;
            }

            CacheEntry classification = s_classifications.GetValue(symbol, CreateCacheEntry);
            return classification.ExperimentalAttributeClasses.Any(
                attributeClass => IsIncluded(attributeClass, attributeDataSymbolFilter))
                ? ApiStability.Experimental
                : ApiStability.Stable;
        }

        private static CacheEntry CreateCacheEntry(ISymbol symbol)
        {
            List<INamedTypeSymbol> experimentalAttributeClasses = [];
            AddExperimentalAttributeClasses(symbol, experimentalAttributeClasses);

            if (symbol is IMethodSymbol { AssociatedSymbol: { } associatedSymbol })
            {
                AddExperimentalAttributeClasses(associatedSymbol, experimentalAttributeClasses);
            }

            if (symbol is not IModuleSymbol && symbol.ContainingModule is { } containingModule)
            {
                AddExperimentalAttributeClasses(containingModule, experimentalAttributeClasses);
            }

            if (symbol is not IAssemblySymbol && symbol.ContainingAssembly is { } containingAssembly)
            {
                AddExperimentalAttributeClasses(containingAssembly, experimentalAttributeClasses);
            }

            return new([.. experimentalAttributeClasses]);
        }

        private static void AddExperimentalAttributeClasses(ISymbol symbol, List<INamedTypeSymbol> experimentalAttributeClasses)
        {
            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                if (IsExperimentalAttribute(attribute) && attribute.AttributeClass is { } attributeClass)
                {
                    experimentalAttributeClasses.Add(attributeClass);
                }
            }
        }

        public static bool IsExperimentalAttribute(AttributeData attribute) =>
            attribute.AttributeClass is
            {
                MetadataName: ExperimentalAttributeName,
                ContainingNamespace:
                {
                    Name: "CodeAnalysis",
                    ContainingNamespace:
                    {
                        Name: "Diagnostics",
                        ContainingNamespace:
                        {
                            Name: "System",
                            ContainingNamespace.IsGlobalNamespace: true
                        }
                    }
                }
            } &&
            HasExpectedConstructor(attribute);

        private static bool HasExpectedConstructor(AttributeData attribute)
        {
            // ApiCompat commonly loads metadata without assembly references, in which case Roslyn cannot bind the constructor.
            return attribute.AttributeConstructor is null ||
                attribute.AttributeConstructor.Parameters is [{ Type.SpecialType: SpecialType.System_String }];
        }

        private static bool IsIncluded(INamedTypeSymbol attributeClass, ISymbolFilter filter)
        {
            if (attributeClass is not IErrorTypeSymbol)
            {
                return filter.Include(attributeClass);
            }

            return filter switch
            {
                AccessibilitySymbolFilter => true,
                CompositeSymbolFilter { Mode: CompositeSymbolFilterMode.And } composite =>
                    composite.Filters.All(innerFilter => IsIncluded(attributeClass, innerFilter)),
                CompositeSymbolFilter { Mode: CompositeSymbolFilterMode.Or } composite =>
                    composite.Filters.Any(innerFilter => IsIncluded(attributeClass, innerFilter)),
                _ => filter.Include(attributeClass)
            };
        }

        private sealed class CacheEntry(INamedTypeSymbol[] experimentalAttributeClasses)
        {
            public INamedTypeSymbol[] ExperimentalAttributeClasses { get; } = experimentalAttributeClasses;
        }
    }
}
