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
            return classification.ExperimentalAttributeClasses.Any(attributeDataSymbolFilter.Include)
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
            };

        private sealed class CacheEntry(INamedTypeSymbol[] experimentalAttributeClasses)
        {
            public INamedTypeSymbol[] ExperimentalAttributeClasses { get; } = experimentalAttributeClasses;
        }
    }
}
